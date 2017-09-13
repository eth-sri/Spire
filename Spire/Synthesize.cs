/*
 * This source file is part of Spire (Synthesis of ProbabIlistic pRivacy Enforcements).
 * For more information, see the Spire project website at:
 *     http://www.srl.inf.ethz.ch/probabilistic-security
 * Copyright 2017 Software Reliability Lab, ETH Zurich
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Spire
{
    public static class Synthesize
    {
        private static void CheckArguments(List<int[]> outputs, Dictionary<int[], Rational> outputProbabilities, IEnumerable<SecurityAssertion> policy)
        {
            if (!(outputs.Count() > 0))
            {
                throw new ArgumentException($"The list {nameof(outputs)} can not be empty.");
            }

            if (!(outputProbabilities.Count == outputs.Count))
            {
                throw new ArgumentException($"Wrong number of key/value pairs in the dictionary {nameof(outputProbabilities)}.");
            }

            foreach (SecurityAssertion assertion in policy)
            {
                if (!(assertion.SecretProbabilitiesGivenOutput.Count == outputs.Count))
                {
                    throw new ArgumentException($"Wrong number of key/value pairs in the dictionary {nameof(assertion.SecretProbabilitiesGivenOutput)}.");
                }

                if (!(assertion.LowerBound >= 0 && assertion.LowerBound <= 1))
                {
                    throw new ArgumentException($"The value of {nameof(assertion.LowerBound)} must be between 0 and 1.");
                }

                if (!(assertion.UpperBound >= 0 && assertion.UpperBound <= 1))
                {
                    throw new ArgumentException($"The value of {nameof(assertion.UpperBound)} must be between 0 and 1.");
                }

                if (!(assertion.LowerBound <= assertion.UpperBound))
                {
                    throw new ArgumentException($"The value of {nameof(assertion.LowerBound)} must be less than or equal to {nameof(assertion.UpperBound)}.");
                }
            }
        }

        public static EquivalenceRelationClasses SyntheiszeEquivalenceRelationZ3(
            List<int[]> outputs,
            Dictionary<int[], Rational> outputProbabilities,
            IEnumerable<SecurityAssertion> policy,
            string optimizationGoal,
            string smtLibFile,
            int timeout = -1)
        {
            CheckArguments(outputs, outputProbabilities, policy);

            int n = outputs.Count();

            var settings = new Dictionary<string, string>() { { "model", "true" } };
            if (timeout > 0)
            {
                settings.Add("timeout", timeout.ToString());
            }

            using (var ctx = new Microsoft.Z3.Context(settings))
            {
                // Declare constants class_0, ..., class_n representing the classes of outputs
                Dictionary<int[], IntExpr> @class = new Dictionary<int[], IntExpr>();
                foreach (int[] o in outputs)
                {
                    @class[o] = (IntExpr)ctx.MkConst(ctx.MkSymbol("class_" + string.Join("_", o.Select((int i) => i.ToString()))), ctx.IntSort);
                }

                // Constraint each class_i to be in 1, ..., n
                List<BoolExpr> rangeConstraintArray = new List<BoolExpr>();
                foreach (int[] o in outputs)
                {
                    rangeConstraintArray.Add(ctx.MkAnd(
                        ctx.MkGe(@class[o], ctx.MkInt(0)),
                        ctx.MkLe(@class[o], ctx.MkInt(n - 1))
                        ));
                }
                BoolExpr rangeConstraint = ctx.MkAnd(rangeConstraintArray.ToArray());

                List<BoolExpr> policyConstraints = new List<BoolExpr>();
                foreach (SecurityAssertion securityAssertion in policy)
                {
                    // Each class has the conditional probability in the given range
                    BoolExpr[] classProbConstraintArray = new BoolExpr[n];
                    for (int j = 0; j < n; j++)
                    {
                        // Top sum
                        List<ArithExpr> summantTop = new List<ArithExpr>();
                        foreach (int[] o in outputs)
                        {
                            summantTop.Add(ctx.MkMul(
                                (ArithExpr)ctx.MkITE(ctx.MkEq(@class[o], ctx.MkInt(j)), ctx.MkReal(1), ctx.MkReal(0)),
                                ctx.MkDiv(ctx.MkReal(securityAssertion.SecretProbabilitiesGivenOutput[o].Numerator.ToString()), ctx.MkReal(securityAssertion.SecretProbabilitiesGivenOutput[o].Denominator.ToString())),
                                ctx.MkDiv(ctx.MkReal(outputProbabilities[o].Numerator.ToString()), ctx.MkReal(outputProbabilities[o].Denominator.ToString()))
                            ));
                        }
                        ArithExpr sumTop = ctx.MkAdd(summantTop.ToArray());

                        // Bottom sum
                        List<ArithExpr> summantBottom = new List<ArithExpr>();
                        foreach (int[] o in outputs)
                        {
                            summantBottom.Add(ctx.MkMul(
                                (ArithExpr)ctx.MkITE(ctx.MkEq(@class[o], ctx.MkInt(j)), ctx.MkReal(1), ctx.MkReal(0)),
                                ctx.MkDiv(ctx.MkReal(outputProbabilities[o].Numerator.ToString()), ctx.MkReal(outputProbabilities[o].Denominator.ToString()))
                            ));
                        }
                        ArithExpr sumBottom = ctx.MkAdd(summantBottom.ToArray());

                        ArithExpr classProbability = ctx.MkDiv(sumTop, sumBottom);

                        classProbConstraintArray[j] = ctx.MkAnd(
                            ctx.MkGe(classProbability, ctx.MkReal(securityAssertion.LowerBound.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                            ctx.MkLe(classProbability, ctx.MkReal(securityAssertion.UpperBound.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                            );
                    }
                    BoolExpr classProbConstraint = ctx.MkAnd(classProbConstraintArray);
                    policyConstraints.Add(classProbConstraint);
                }

                // Final constraint
                BoolExpr constraint = ctx.MkAnd(rangeConstraint, ctx.MkAnd(policyConstraints.ToArray()));

                // Classes count
                ArithExpr[] isClassNonempty = new ArithExpr[n];
                for (int j = 0; j < n; j++)
                {
                    List<BoolExpr> isInClass = new List<BoolExpr>();
                    foreach (int[] o in outputs)
                    {
                        isInClass.Add(ctx.MkEq(@class[o], ctx.MkInt(j)));
                    }
                    isClassNonempty[j] = (ArithExpr)ctx.MkITE(ctx.MkOr(isInClass.ToArray()), ctx.MkReal(1), ctx.MkReal(0));
                }
                ArithExpr classesCount = ctx.MkAdd(isClassNonempty);

                // Singletons count
                ArithExpr[] isClassSingleton = new ArithExpr[n];
                for (int j = 0; j < n; j++)
                {
                    List<ArithExpr> isInClass = new List<ArithExpr>();
                    foreach (int[] o in outputs)
                    {
                        isInClass.Add((ArithExpr)ctx.MkITE(ctx.MkEq(@class[o], ctx.MkInt(j)), ctx.MkReal(1), ctx.MkReal(0)));
                    }
                    isClassSingleton[j] = (ArithExpr)ctx.MkITE(ctx.MkEq(ctx.MkAdd(isInClass.ToArray()), ctx.MkReal(1)), ctx.MkReal(1), ctx.MkReal(0));
                }
                ArithExpr singletonClassesCount = ctx.MkAdd(isClassSingleton);

                // Solve
                Optimize opt = ctx.MkOptimize();
                opt.Assert(constraint);
                if (optimizationGoal == "classes")
                {
                    opt.MkMaximize(classesCount);
                }
                else if (optimizationGoal == "singletons")
                {
                    opt.MkMaximize(singletonClassesCount);
                }
                else
                {
                    throw new Exception("Optimization goal " + optimizationGoal + " is not supported.");
                }

                if (!string.IsNullOrWhiteSpace(smtLibFile))
                {
                    Console.WriteLine("SAVING SMTLIB TO FILE...");
                    Console.WriteLine(smtLibFile);
                    File.WriteAllText(smtLibFile, opt.ToString());
                }
                else
                {
                    Console.WriteLine("SMTLIB file not generated.");
                }

                Status status = opt.Check();
                if (status == Status.SATISFIABLE)
                {
                    Model m = opt.Model;

                    var dict = new Dictionary<int, List<int[]>>();
                    foreach (int[] o in outputs)
                    {
                        int k = ((IntNum)m.Evaluate(@class[o])).Int;
                        if (!dict.ContainsKey(k))
                        {
                            dict.Add(k, new List<int[]>());
                        }
                        dict[k].Add(o);
                    }
                    List<List<int[]>> classes = dict.Values.ToList();
                    return new EquivalenceRelationClasses() { Classes = classes };
                }
                else if (status == Status.UNSATISFIABLE)
                {
                    throw new Exception("The instance does not have a solution.");
                }
                else
                {
                    throw new Exception("Z3 returned UNKNOWN.");
                }
            }
        }

        private static double EuclideanDistance(double[] first, double[] second)
        {
            if (!(first.Length == second.Length))
            {
                throw new ArgumentException($"The length of {nameof(first)} must be equal to {nameof(second)}");
            }

            double sum = 0;
            for (int i = 0; i < first.Length; i++)
            {
                double difference = first[i] - second[i];
                sum += difference * difference;
            }
            return Math.Sqrt(sum);
        }

        private static double DistanceFromInterval(double[] point, List<SecurityAssertion> policy)
        {
            if (!(point.Length == policy.Count))
            {
                throw new ArgumentException($"The length of {nameof(point)} must be equal to the count of {nameof(policy)}");
            }

            double[] extremePoint = new double[point.Length];
            for (int i = 0; i < point.Length; i++)
            {
                if (point[i] < policy[i].LowerBound)
                {
                    extremePoint[i] = policy[i].LowerBound;
                }
                else if (point[i] <= policy[i].UpperBound)
                {
                    extremePoint[i] = point[i];
                }
                else
                {
                    extremePoint[i] = policy[i].UpperBound;
                }
            }

            return EuclideanDistance(point, extremePoint);
        }

        private static bool ClassViolatesSomeAssertion(Rational[] secretProbabilities, IEnumerable<SecurityAssertion> policy)
        {
            int i = 0;
            foreach (SecurityAssertion sa in policy)
            {
                if (secretProbabilities[i].ToDouble() < sa.LowerBound || secretProbabilities[i].ToDouble() > sa.UpperBound)
                {
                    return true;
                }
                i++;
            }
            return false;
        }

        public static EquivalenceRelationClasses SyntheiszeEquivalenceRelationHeuristicOptSingletons(
            List<int[]> outputs,
            Dictionary<int[], Rational> outputProbabilities,
            List<SecurityAssertion> policy)
        {
            CheckArguments(outputs, outputProbabilities, policy);

            var @class = new List<int[]>();
            var singletons = new List<int[]>();

            Rational[] class_secret_probability = new Rational[policy.Count()];
            for (int i = 0; i < class_secret_probability.Length; i++)
            {
                class_secret_probability[i] = new Rational(0, 1);
            }
            Rational class_probability_mass = new Rational(0, 1);
            {
                Rational[] numerator = new Rational[policy.Count()];
                for (int i = 0; i < numerator.Length; i++)
                {
                    numerator[i] = new Rational(0, 1);
                }

                foreach (int[] output in outputs)
                {
                    bool violates = false;
                    foreach (SecurityAssertion assert in policy)
                    {
                        if (assert.SecretProbabilitiesGivenOutput[output].ToDouble() < assert.LowerBound ||
                            assert.SecretProbabilitiesGivenOutput[output].ToDouble() > assert.UpperBound)
                        {
                            violates = true;
                        }
                    }
                    if (violates && outputProbabilities[output].ToDouble() > 0) // ignore zero prob. outputs
                    {
                        @class.Add(output);
                        class_probability_mass += outputProbabilities[output];
                        int i = 0;
                        foreach (SecurityAssertion sa in policy)
                        {
                            numerator[i] += sa.SecretProbabilitiesGivenOutput[output] * outputProbabilities[output];
                            i++;
                        }
                    }
                    else
                    {
                        singletons.Add(output);
                    }
                }

                for (int i = 0; i < numerator.Length; i++)
                {
                    class_secret_probability[i] = numerator[i] / class_probability_mass;
                }
            }

            // while C still violates
            // pick the output o from O that moves C as close to sat. as possible, add it to C
            while (singletons.Count > 0 && ClassViolatesSomeAssertion(class_secret_probability, policy))
            {
                int new_singleton_index = 0;
                Rational new_class_probability_mass = class_probability_mass + outputProbabilities[singletons[0]];
                Rational[] new_class_secret_porobability = new Rational[policy.Count()];
                for (int i = 0; i < new_class_secret_porobability.Length; i++)
                {
                    new_class_secret_porobability[i] =
                        (class_secret_probability[i] * class_probability_mass +
                        policy[i].SecretProbabilitiesGivenOutput[singletons[0]] * outputProbabilities[singletons[0]])
                        /
                        new_class_probability_mass;
                }
                double euclid_distance = DistanceFromInterval(new_class_secret_porobability.Select(r => r.ToDouble()).ToArray(), policy);

                for (int i = 1; i < singletons.Count; i++)
                {
                    Rational new_class_probability_mass_2 = class_probability_mass + outputProbabilities[singletons[i]];
                    Rational[] new_class_secret_porobability_2 = new Rational[policy.Count()];
                    for (int __i = 0; __i < new_class_secret_porobability_2.Length; __i++)
                    {
                        new_class_secret_porobability_2[__i] =
                            (class_secret_probability[__i] * class_probability_mass +
                            policy[__i].SecretProbabilitiesGivenOutput[singletons[i]] * outputProbabilities[singletons[i]])
                            /
                            new_class_probability_mass_2; // added the two
                    }
                    double euclid_distance_2 = DistanceFromInterval(new_class_secret_porobability_2.Select(r => r.ToDouble()).ToArray(), policy);

                    if (euclid_distance_2 < euclid_distance)
                    {
                        new_singleton_index = i;
                        new_class_probability_mass = new_class_probability_mass_2;
                        new_class_secret_porobability = new_class_secret_porobability_2;
                        euclid_distance = euclid_distance_2;
                    }
                }

                @class.Add(singletons[new_singleton_index]);
                singletons.RemoveAt(new_singleton_index);

                class_probability_mass = new_class_probability_mass;
                class_secret_probability = new_class_secret_porobability;
            }

            EquivalenceRelationClasses result = new EquivalenceRelationClasses();
            result.Classes = new List<List<int[]>>();
            result.Classes.Add(@class);
            result.Classes.AddRange(singletons.Select(o => new List<int[]>() { o }));

            return result;
        }

        public static EquivalenceRelationClasses SyntheiszeEquivalenceRelationHeuristicOptClasses(
            List<int[]> outputs,
            Dictionary<int[], Rational> outputProbabilities,
            List<SecurityAssertion> policy)
        {
            CheckArguments(outputs, outputProbabilities, policy);

            foreach (SecurityAssertion securityAssertion in policy)
            {
                Rational secretProbInWhole = new Rational(0, 1);
                foreach (int[] o in outputs)
                {
                    secretProbInWhole += securityAssertion.SecretProbabilitiesGivenOutput[o] * outputProbabilities[o];
                }
                if (secretProbInWhole.ToDouble() < securityAssertion.LowerBound || secretProbInWhole.ToDouble() > securityAssertion.UpperBound)
                {
                    throw new Exception($"The probability of secret ({securityAssertion.PsiConstraint}) in the prior {secretProbInWhole} is not in the desired interval [{securityAssertion.LowerBound}, {securityAssertion.UpperBound}].");
                }
            }

            List<List<int[]>> classes = new List<List<int[]>>();
            foreach (int[] o in outputs)
            {
                classes.Add(new List<int[]> { o });
            }

            while (true)
            {
                Rational[] classProbability = new Rational[classes.Count];
                for (int i = 0; i < classes.Count; i++)
                {
                    Rational prob = new Rational(0, 1);
                    foreach (int[] o in classes[i])
                    {
                        prob += outputProbabilities[o];
                    }
                    classProbability[i] = prob;
                }

                Rational[][] secretProbability = new Rational[classes.Count][];
                for (int i = 0; i < classes.Count; i++)
                {
                    secretProbability[i] = new Rational[policy.Count];
                }
                for (int j = 0; j < policy.Count; j++)
                {
                    for (int i = 0; i < classes.Count; i++)
                    {
                        Rational numerator = new Rational(0, 1);
                        foreach (int[] o in classes[i])
                        {
                            numerator += policy[j].SecretProbabilitiesGivenOutput[o] * outputProbabilities[o];
                        }
                        secretProbability[i][j] = numerator / classProbability[i];
                    }
                }

                bool allInInterval = true;
                for (int j = 0; j < policy.Count; j++)
                {
                    for (int i = 0; i < classes.Count; i++)
                    {
                        double d = secretProbability[i][j].ToDouble();
                        if (d < policy[j].LowerBound || d > policy[j].UpperBound)
                        {
                            allInInterval = false;
                        }
                    }
                }
                if (allInInterval)
                {
                    break;
                }

                double[] center = new double[policy.Count];
                for (int j = 0; j < policy.Count; j++)
                {
                    center[j] = (policy[j].LowerBound + policy[j].UpperBound) / 2;
                }

                int maxDiffIndex = 0;
                double maxDiff = DistanceFromInterval(secretProbability[maxDiffIndex].Select((Rational r) => r.ToDouble()).ToArray(), policy);
                for (int i = 0; i < classes.Count; i++)
                {
                    double currentDiff = DistanceFromInterval(secretProbability[i].Select((Rational r) => r.ToDouble()).ToArray(), policy);

                    if (currentDiff > maxDiff)
                    {
                        maxDiffIndex = i;
                        maxDiff = currentDiff;
                    }
                }

                double[] distanceFromBox = new double[classes.Count];
                double distanceFromBoxMin = double.PositiveInfinity;
                int distanceFromBoxMinIndex = 0;

                double[] distanceFromCenter = new double[classes.Count];
                double distanceFromCenterMin = double.PositiveInfinity;
                int distanceFromCenterMinIndex = 0;

                for (int i = 0; i < classes.Count; i++)
                {
                    if (i == maxDiffIndex)
                    {
                        distanceFromBox[i] = double.PositiveInfinity;
                        distanceFromCenter[i] = double.PositiveInfinity;
                        continue;
                    }

                    double[] point = new double[policy.Count];
                    for (int j = 0; j < policy.Count; j++)
                    {
                        point[j] = ((secretProbability[maxDiffIndex][j] * classProbability[maxDiffIndex] + secretProbability[i][j] * classProbability[i]) / (classProbability[maxDiffIndex] + classProbability[i])).ToDouble();
                    }

                    double distanceFromBoxCurrent = DistanceFromInterval(point, policy);
                    distanceFromBox[i] = distanceFromBoxCurrent;
                    if (distanceFromBoxCurrent < distanceFromBoxMin)
                    {
                        distanceFromBoxMin = distanceFromBoxCurrent;
                        distanceFromBoxMinIndex = i;
                    }

                    double distanceFromCenterCurrent = EuclideanDistance(point, center);
                    distanceFromCenter[i] = distanceFromCenterCurrent;
                    if (distanceFromCenterCurrent < distanceFromCenterMin)
                    {
                        distanceFromCenterMin = distanceFromCenterCurrent;
                        distanceFromCenterMinIndex = i;
                    }
                }

                // Now check if some are inside, if yes, pick from these the one closest to the center.
                // If none are inside, pick the one closest to the box.
                int indexOfChoice;
                if (distanceFromBoxMin > 0)
                {
                    // No points inside the box, we just pick the closest point to the box then
                    indexOfChoice = distanceFromBoxMinIndex;
                }
                else
                {
                    // We are picking from points inside the box. We choose the one closest to the center.
                    indexOfChoice = distanceFromCenterMinIndex;
                }

                // Join maxDiff and minDiff
                List<List<int[]>> newClasses = new List<List<int[]>>();
                for (int i = 0; i < classes.Count; i++)
                {
                    if (i != maxDiffIndex && i != indexOfChoice)
                    {
                        newClasses.Add(classes[i]);
                    }
                }
                List<int[]> newClass = new List<int[]>();
                newClass.AddRange(classes[maxDiffIndex]);
                newClass.AddRange(classes[indexOfChoice]);
                newClasses.Add(newClass);

                classes = newClasses;
            }

            return new EquivalenceRelationClasses() { Classes = classes };
        }
    }
}
