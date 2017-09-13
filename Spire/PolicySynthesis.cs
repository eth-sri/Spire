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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Spire
{
    class PolicySynthesis
    {
        public DateTimeOffset started { get; set; }

        public int psiTimeout { get; set; }

        public string PsiPath { get; set; }

        public string PriorPath { get; set; }

        public string ProgramPath { get; set; }

        private StreamWriter log = null;

        private StreamWriter csv = null;

        private void WriteLogEntry(string label, object value)
        {
            log?.WriteLine("{0}: {1}", label, value);
            csv?.Write("{0},", value);
            Console.WriteLine(">>> {0}: {1}", label, value);
        }

        /// <summary>
        /// Runs psi and returns the output.
        /// </summary>
        private string RunPsi(string arguments)
        {
            double time = 0;
            return RunPsi(arguments, out time);
        }

        private string RunPsi(string arguments, out double time)
        {
            var sw = Stopwatch.StartNew();
            Process process = new Process();
            process.StartInfo.FileName = PsiPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.Start();
            string strOutput = process.StandardOutput.ReadToEnd();

            int msElapsed = (int)(DateTimeOffset.Now - started).TotalMilliseconds;
            int msRemaining = (psiTimeout == int.MaxValue ? int.MaxValue : psiTimeout - msElapsed);

            if (!process.WaitForExit(msRemaining))
            {
                process.Kill();
                throw new Exception(string.Format("Psi timed out. Total timeout: {0}, timeout for this run {1}.", psiTimeout, msRemaining));
            }
            if (process.ExitCode != 0)
            {
                throw new Exception("Psi returned a non-zero exit code.");
            }
            time = sw.Elapsed.TotalSeconds;
            return strOutput;
        }

        private string[] GetSummary(string fileName)
        {
            string summary = RunPsi(fileName + " --summarize=[name,arg-arity,ret-arity]");
            List<string> lines = new List<string>();
            using (StringReader sr = new StringReader(summary))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }
            }
            return lines.ToArray();
        }

        private string ComputePosterior(string fileName, string[] template)
        {
            List<string> file = new List<string>();
            file.AddRange(File.ReadAllLines(PriorPath));
            file.AddRange(File.ReadAllLines(ProgramPath));
            file.AddRange(template);

            File.WriteAllLines(fileName, file);

            string psiOutput = RunPsi(@"--raw --noboundscheck --dp " + fileName);

            return psiOutput;
        }

        private Rational GetProbabilitySimplifyWithSubstitution(string psiOutput, int[] o)
        {
            for (int i = 0; i < o.Length; i++)
            {
                psiOutput = psiOutput.Replace("o" + i, o[i].ToString());
            }

            using (var ctx = new Microsoft.Z3.Context(new Dictionary<string, string>() { { "model", "true" } }))
            {
                BoolExpr ex = ctx.ParseSMTLIB2String("(assert (= 0 " + psiOutput + "))");
                Expr expression = ex.Args[1];
                if (expression is RealExpr)
                {
                    RealExpr prob = (RealExpr)expression;
                    RatNum simplified = (RatNum)prob.Simplify();
                    return new Rational(simplified.Numerator.Int, simplified.Denominator.Int);
                }
                else
                {
                    IntExpr prob = (IntExpr)expression;
                    IntNum simplified = (IntNum)prob.Simplify();
                    return new Rational(simplified.Int, 1);
                }
            }
        }

        private int[] SimulateWithSampleFrom(string program, int inputArity, int outputArity, string particularInput, string fileNamePosterior, string fileNameSampleFrom)
        {
            string input = string.Join(", ", Enumerable.Range(0, inputArity).Select((int i) => "input" + i));
            string output = string.Join(", ", Enumerable.Range(0, outputArity).Select((int i) => "output" + i));

            var template = new string[] {
                    "def main()",
                    "{",
                    "\t(" + input + ") := " + particularInput + ";",
                    "\t(" + output +  ") := program(" + input + ");",
                    "\treturn (" + output + ");",
                    "}"
                };

            List<string> file = new List<string>();
            file.AddRange(File.ReadLines(program));
            file.AddRange(template);
            File.WriteAllLines(fileNamePosterior, file);

            double time;
            string psiOutput = RunPsi(@"--raw --noboundscheck " + fileNamePosterior, out time).Trim();

            var template2 = new string[]
            {
                "def main()",
                "{",
                "\treturn sampleFrom(\"(" + output + ") => " + psiOutput + "\");",
                "}"
            };
            File.WriteAllLines(fileNameSampleFrom, template2);

            string psiOutput2 = RunPsi(@"--simulate " + fileNameSampleFrom, out time).Trim();

            if (outputArity == 1)
            {
                return new int[] { int.Parse(psiOutput2) };
            }
            else
            {
                int leftParPos = psiOutput2.IndexOf('(');
                int rifhtParPos = psiOutput2.IndexOf(')');
                string tuple = psiOutput2.Substring(leftParPos + 1, rifhtParPos - leftParPos - 1);
                string[] split = tuple.Split(',').Select(s => s.Trim()).ToArray();
                return split.Select(s => int.Parse(s)).ToArray();
            }
        }

        List<int[]> GetDiagonal(int dimension, int diagonal)
        {
            List<int[]> doneSoFar = new List<int[]> { new int[] { } };

            while (doneSoFar[0].Length < dimension - 1)
            {
                List<int[]> list = new List<int[]>();
                foreach(int[] point in doneSoFar)
                {
                    for (int i = 0; i <= diagonal - point.Sum(); i++)
                    {
                        int[] newPoint = new int[point.Length + 1];
                        Array.Copy(point, newPoint, point.Length);
                        newPoint[newPoint.Length - 1] = i;
                        list.Add(newPoint);
                    }
                }
                doneSoFar = list;
            }

            {
                List<int[]> list = new List<int[]>();
                foreach (int[] point in doneSoFar)
                {
                    int[] newPoint = new int[point.Length + 1];
                    Array.Copy(point, newPoint, point.Length);
                    newPoint[newPoint.Length - 1] = diagonal - point.Sum();
                    list.Add(newPoint);
                }
                doneSoFar = list;
            }

            return doneSoFar;
        }

        /// <summary>
        /// Run psi, and compute the probabilities assigned to the whole range of ?o0, ?o1, ... defined by the template.
        /// </summary>
        private Dictionary<int[], Rational> ComputeProbabilitiesAsDictionary(string fileName, string[] template, int dimension, out double time, bool includeZeroes = false)
        {
            List<string> file = new List<string>();
            file.AddRange(File.ReadAllLines(PriorPath));
            file.AddRange(File.ReadAllLines(ProgramPath));
            file.AddRange(template);

            File.WriteAllLines(fileName, file);

            string psiOutput = RunPsi(@"--lisp --expectation --raw --noboundscheck --dp " + fileName, out time);

            Dictionary<int[], Rational> outputProbability = new Dictionary<int[], Rational>();
            Rational sumOfProbabilities = new Rational(0, 1);
            int diagonal = 0;
            while (sumOfProbabilities.ToDouble() < 1) // Careful about rounding here
            {
                IEnumerable<int[]> outputs = GetDiagonal(dimension, diagonal);

                foreach (int[] o in outputs)
                {
                    Rational prob = GetProbabilitySimplifyWithSubstitution(psiOutput, o);
                    if (includeZeroes || prob.Numerator > 0)
                    {
                        outputProbability.Add(o, prob);
                    }
                    sumOfProbabilities += prob;
                }

                diagonal++;
            }

            return outputProbability;
        }
        
        private Dictionary<int[], Rational> ComputeProbabilitiesAsDictionaryFromOutputSet(string fileName, string[] template, IEnumerable<int[]> outputs, out double time, bool includeZeroes = false)
        {
            List<string> file = new List<string>();
            file.AddRange(File.ReadAllLines(PriorPath));
            file.AddRange(File.ReadAllLines(ProgramPath));
            file.AddRange(template);

            File.WriteAllLines(fileName, file);

            string psiOutput = RunPsi(@"--lisp --expectation --raw --noboundscheck --dp " + fileName, out time);

            Dictionary<int[], Rational> outputProbability = new Dictionary<int[], Rational>();
            foreach (int[] o in outputs)
            {
                Rational prob = GetProbabilitySimplifyWithSubstitution(psiOutput, o);
                if (includeZeroes || prob.Numerator > 0)
                {
                    outputProbability.Add(o, prob);
                }
            }

            return outputProbability;
        }

        private List<List<int>> MultidimensionalInterval_Impl(List<List<int>> lists, int current, int[] rangeFrom, int[] rangeTo)
        {
            List<List<int>> extendedLists = new List<List<int>>();

            for (int i = rangeFrom[current]; i <= rangeTo[current]; i++)
            {
                foreach (List<int> list in lists)
                {
                    List<int> newList = new List<int>(list);
                    newList.Insert(0, i);
                    extendedLists.Add(newList);
                }
            }

            if (current > 0)
            {
                return MultidimensionalInterval_Impl(extendedLists, current - 1, rangeFrom, rangeTo);
            }
            else
            {
                return extendedLists;
            }
        }

        /// <summary>
        /// Computes the distribution of the given outputs.
        /// </summary>
        public Dictionary<int[], Rational> ComputeOutputProbabilities(string fileName, int inputArity, int outputArity, string prior, out double time)
        {
            string input = string.Join(", ", Enumerable.Range(0, inputArity).Select((int i) => "input" + i));

            var template = new string[] {
                    "def main()",
                    "{",
                    "\t(" + input + ") := " + prior + ";",
                    "\t(" + string.Join(", ", Enumerable.Range(0, outputArity).Select((int i) => "output" + i)) +  ") := program(" + input + ");",
                    "\treturn (" + string.Join(" && ", Enumerable.Range(0, outputArity).Select((int i) => "output" + i + " == ?o" + i)) + ");",
                    "}"
                };

            Dictionary<int[], Rational> distribution = ComputeProbabilitiesAsDictionary(fileName, template, outputArity, out time, false);

            return distribution;
        }

        /// <summary>
        /// Computes the probability of secret conditioned by outputs.
        /// </summary>
        public Dictionary<int[], Rational> ComputeSecretProbabilitiesGivenOutput(string fileName, string psiFormula, int inputArity, int outputArity, IEnumerable<int[]> outputs, string prior, out double time)
        {
            string input = string.Join(", ", Enumerable.Range(0, inputArity).Select((int i) => "input" + i));

            string[] template = new string[] {
                "def main()",
                "{",
                "\t(" + input + ") := " + prior + ";",
                "\t(" + string.Join(", ", Enumerable.Range(0, outputArity).Select((int i) => "output" + i)) +  ") := program(" + input + ");",
                "\tobserve(" + string.Join(" && ", Enumerable.Range(0, outputArity).Select((int i) => "output" + i + " == ?o" + i)) + ");",
                "\treturn (" + psiFormula + ");",
                "}"
            };

            Dictionary<int[], Rational> distribution = ComputeProbabilitiesAsDictionaryFromOutputSet(fileName, template, outputs, out time, true);

            return distribution;
        }

        public string ComputeUpdatedPrior(string fileName, int inputArity, int outputArity, IEnumerable<int[]> @class, string prior)
        {
            IEnumerable<string> equalities = @class.Select((int[] c) => "(" + string.Join(" && ", c.Select((int i, int index) => "output" + index + " == " + i)) + ")");
            string input = string.Join(", ", Enumerable.Range(0, inputArity).Select((int i) => "input" + i));
            string[] template3 = new string[] {
                        "def main()",
                        "{",
                        "\t(" + input + ") := " + prior + ";",
                        "\t(" + string.Join(", ", Enumerable.Range(0, outputArity).Select((int i) => "output" + i)) +  ") := program(" + input + ");",
                        "\tobserve(" + string.Join(" || ", equalities) + ");",
                        "\treturn (" + input + ");",
                        "}"
                    };

            return "(sampleFrom(\"(r) => " + ComputePosterior(fileName, template3).Trim() + "\") : " + string.Join(" x ", Enumerable.Repeat("R", inputArity)) + ")";
        }

        static void ParseSummary(string[] lines, string expectedName, out int inArity, out int outArity)
        {
            if (lines.Length != 1)
            {
                throw new Exception($"The {expectedName} psi file must contain exactly one function.");
            }

            string[] split = lines[0].Split(',');

            if (split.Length != 3)
            {
                throw new Exception("Wrong psi summary output format.");
            }

            if (split[0] != expectedName)
            {
                throw new Exception($"The function in the {expectedName} psi file must have name {expectedName}.");
            }

            inArity = int.Parse(split[1]);
            outArity = int.Parse(split[2]);
        }

        public static void IteratePolicySynthesis(
            string psiPath,
            string priorPath,
            string programPath,
            string policyPath,
            string tempPrefix,
            int iterationsCount,
            string particularInput,
            StreamWriter log,
            StreamWriter csv,
            StreamWriter iterationLog,
            int psiTimeout,
            int z3Timeout,
            string optimizationGoal,
            string smtLibFile,
            bool iterateWithHeur)
        {
            var totalTime = Stopwatch.StartNew();

            iterationLog?.WriteLine("iteration,outputs,classes,classes ratio");

            var p = new PolicySynthesis();

            p.started = DateTimeOffset.Now;
            p.psiTimeout = psiTimeout;

            p.PsiPath = psiPath;
            p.PriorPath = priorPath;
            p.ProgramPath = programPath;

            p.log = log;
            p.csv = csv;

            int inputArity;
            int outputArity;

            int priorInputArity;
            int priorOutputArity;
            int programInputArity;
            int programOutputArity;

            ParseSummary(p.GetSummary(p.PriorPath), "prior", out priorInputArity, out priorOutputArity);
            ParseSummary(p.GetSummary(p.ProgramPath), "program", out programInputArity, out programOutputArity);

            if (priorInputArity != 0)
            {
                throw new Exception("Prior input arity must be 0.");
            }

            if (priorOutputArity != programInputArity)
            {
                throw new Exception("Prior output arity does not match the program input arity.");
            }

            inputArity = programInputArity;
            outputArity = programOutputArity;

            if (!(inputArity > 0))
            {
                throw new ArgumentException($"The argument {nameof(inputArity)} must be greater than zero.", nameof(inputArity));
            }

            if (!(outputArity > 0))
            {
                throw new ArgumentException($"The argument {nameof(outputArity)} must be greater than zero.", nameof(outputArity));
            }

            if (!(iterationsCount > 0))
            {
                throw new ArgumentException($"The argument {nameof(iterationsCount)} must be greater than zero.", nameof(iterationsCount));
            }
            
            p.WriteLogEntry("Input arity", programInputArity);
            p.WriteLogEntry("Output arity", programOutputArity);

            string prior = "prior()";

            for (int iteration = 0; iteration < iterationsCount; iteration++)
            {
                Console.WriteLine();
                Console.WriteLine(" ===== Iteration {0} ===== ", iteration);
                Console.WriteLine();

                Dictionary<int[], Rational> outputProbabilities;
                double timeOutputs;
                outputProbabilities = p.ComputeOutputProbabilities(tempPrefix + "_iteration_" +  iteration + "_outputProbabilities.psi", inputArity, outputArity, prior, out timeOutputs);
                List<int[]> outputs = outputProbabilities.Keys.ToList();
                Rational outputProbabilitiesSum = new Rational(0, 1);
                foreach (var kvp in outputProbabilities)
                {
                    Console.WriteLine("Pr(O = ({0})) = {1} = {2}", string.Join(", ", kvp.Key.Select((int i) => i.ToString())), kvp.Value, kvp.Value.ToDouble());
                    outputProbabilitiesSum += kvp.Value;
                }
                Console.WriteLine();
                p.WriteLogEntry("Output probabilities psi call", timeOutputs);
                Console.WriteLine();

                if (!(outputProbabilitiesSum.Numerator == 1 && outputProbabilitiesSum.Denominator == 1))
                {
                    throw new Exception("The output probabilities do not sum to 1.");
                }

                string[] securityAssertions = File.ReadAllLines(policyPath);
                List<SecurityAssertion> policy = new List<SecurityAssertion>();
                double timeSecretsSum = 0;
                for (int assertionNumber = 0; assertionNumber < securityAssertions.Length; assertionNumber++)
                {
                    string securityAssertion = securityAssertions[assertionNumber];

                    string[] split = securityAssertion.Split(';');
                    if (split.Length != 3)
                    {
                        throw new FormatException("Error reading the security policy: wrong number of commas in a line.");
                    }

                    string psiFormula = split[0];
                    double lowerBound = double.Parse(split[1].Trim(), CultureInfo.InvariantCulture);
                    double upperBound = double.Parse(split[2].Trim(), CultureInfo.InvariantCulture);

                    if (!(0 <= lowerBound && lowerBound <= 1))
                    {
                        throw new FormatException("The lower bound of a policy assetion must be between 0 and 1.");
                    }

                    if (!(0 <= upperBound && upperBound <= 1))
                    {
                        throw new FormatException("The upper bound of a policy assetion must be between 0 and 1.");
                    }

                    if (!(lowerBound <= upperBound))
                    {
                        throw new FormatException("The lower bound of a security assertion must be less than or equal to the upper bound.");
                    }

                    Dictionary<int[], Rational> secretProbabilitiesGivenOutput;
                    double timeSecret;
                    secretProbabilitiesGivenOutput = p.ComputeSecretProbabilitiesGivenOutput(tempPrefix + "_iteration_" + iteration + "_secretProbabilities_" + assertionNumber + ".psi", psiFormula, inputArity, outputArity, outputs, prior, out timeSecret);
                    timeSecretsSum += timeSecret;
                    foreach (var kvp in secretProbabilitiesGivenOutput)
                    {
                        Console.WriteLine("Pr({2} | O = ({0})) = {1} = {3}", string.Join(", ", kvp.Key.Select((int i) => i.ToString())), kvp.Value, split[0], kvp.Value.ToDouble());
                    }
                    Console.WriteLine();

                    Rational secretProb = new Rational(0, 1);
                    foreach (int[] o in outputs)
                    {
                        secretProb += secretProbabilitiesGivenOutput[o] * outputProbabilities[o];
                    }
                    Console.WriteLine("Pr({0}) = {1} = {2}", split[0], secretProb, secretProb.ToDouble());
                    Console.WriteLine("Policy range: [{0}, {1}]", lowerBound, upperBound);
                    Console.WriteLine();

                    policy.Add(new SecurityAssertion()
                    {
                        SecretProbabilitiesGivenOutput = secretProbabilitiesGivenOutput,
                        LowerBound = lowerBound,
                        UpperBound = upperBound,
                        PsiConstraint = split[0]
                    });
                }
                p.WriteLogEntry("Secret probabilities psi call/calls", timeSecretsSum);
                Console.WriteLine();

                EquivalenceRelationClasses eq1 = null;
                if (iterateWithHeur == false)
                {
                    try
                    {
                        Console.WriteLine("Z3:");
                        var sw1 = Stopwatch.StartNew();
                        eq1 = Synthesize.SyntheiszeEquivalenceRelationZ3(outputs, outputProbabilities, policy, optimizationGoal, "iter_" + iteration + "_" + smtLibFile, z3Timeout);
                        // SMTLIB file should depend on interations
                        sw1.Stop();
                        Console.WriteLine(eq1.ToString(outputProbabilities, policy));
                        p.WriteLogEntry("Z3 time", sw1.Elapsed.TotalSeconds);
                        p.WriteLogEntry("Z3 classes", eq1.Classes.Count);
                        p.WriteLogEntry("Z3 singletons", eq1.Classes.Count(l => l.Count == 1));
                        p.WriteLogEntry("Z3 Singletons ratio", eq1.Classes.Count(l => l.Count == 1) / (double)eq1.Classes.Count());

                        double expecteddCard = 0;
                        foreach (List<int[]> cl in eq1.Classes)
                        {
                            Rational prob = new Rational(0, 1);
                            foreach (int[] o in cl)
                            {
                                prob += outputProbabilities[o];
                            }
                            expecteddCard += prob.ToDouble() * cl.Count;
                        }
                        p.WriteLogEntry("Z3 Expected class cardinality", expecteddCard);
                        Console.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        Console.WriteLine();
                        p.WriteLogEntry("Z3 time", "n/a");
                        p.WriteLogEntry("Z3 classes", "n/a");
                        p.WriteLogEntry("Z3 singletons", "n/a");
                        p.WriteLogEntry("Z3 Singletons ratio", "n/a");
                        p.WriteLogEntry("Z3 Expected class cardinality", "n/a");
                    }
                }

                EquivalenceRelationClasses eq2 = null;
                try
                {
                    Console.WriteLine("Heuristic:");
                    var sw2 = Stopwatch.StartNew();
                    if (optimizationGoal == "classes")
                    {
                        eq2 = Synthesize.SyntheiszeEquivalenceRelationHeuristicOptClasses(outputs, outputProbabilities, policy);
                    }
                    else if (optimizationGoal == "singletons")
                    {
                        eq2 = Synthesize.SyntheiszeEquivalenceRelationHeuristicOptSingletons(outputs, outputProbabilities, policy);//, optimizationGoal, "");
                    }
                    else
                    {
                        Console.WriteLine("FATAL ERROR!");
                        throw new Exception();
                    }
                    sw2.Stop();
                    Console.WriteLine(eq2.ToString(outputProbabilities, policy));
                    p.WriteLogEntry("Heuristic time", sw2.Elapsed.TotalSeconds);
                    p.WriteLogEntry("Heuristic classes", eq2.Classes.Count);
                    p.WriteLogEntry("Heuristic singletons", eq2.Classes.Count(l => l.Count == 1));
                    p.WriteLogEntry("Heuristic Singletons ratio", eq2.Classes.Count(l => l.Count == 1) / (double)eq2.Classes.Count());

                    double expecteddCard = 0;
                    foreach (List<int[]> cl in eq2.Classes)
                    {
                        Rational prob = new Rational(0, 1);
                        foreach (int[] o in cl)
                        {
                            prob += outputProbabilities[o];
                        }
                        expecteddCard += prob.ToDouble() * cl.Count;
                    }
                    p.WriteLogEntry("Heuristic Expected class cardinality", expecteddCard);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine();
                    p.WriteLogEntry("Heuristic time", "n/a");
                    p.WriteLogEntry("Heuristic classes", "n/a");
                    p.WriteLogEntry("Heuristic singletons", "n/a");
                    p.WriteLogEntry("Heuristic Singletons ratio", "n/a");
                    p.WriteLogEntry("Heuristic Expected class cardinality", "n/a");
                }

                EquivalenceRelationClasses classes = (eq1 != null ? eq1 : eq2);

                iterationLog?.WriteLine("{0},{1},{2},{3}",
                    iteration + 1,
                    outputs.Count,
                    classes.Classes.Count,
                    (double)classes.Classes.Count / outputs.Count);

                if (iteration == iterationsCount - 1)
                {
                    break;
                }

                int[] result = p.SimulateWithSampleFrom(
                    programPath,
                    programInputArity,
                    programOutputArity,
                    "(" + particularInput + ")",
                    tempPrefix + "_iteration_" + iteration + "_simulate_posterior.psi",
                    tempPrefix + "_iteration_" + iteration + "_simulate_sampleFrom.psi");

                Console.WriteLine("SIMULATION: {0}", string.Join(", ", result.Select(i => i.ToString())));

                List<int[]> choice = null;

                foreach (List<int[]> @class in classes.Classes)
                {
                    if (@class.Exists((int[] output) => output.SequenceEqual(result)))
                    {
                        choice = @class;
                    }
                }

                string classObservedString = string.Join(",", choice.Select((int[] a) => "(" + string.Join(",", a.Select((int i) => i.ToString())) + ")"));

                p.WriteLogEntry("Observing output class", "{" + classObservedString + "}");
                p.WriteLogEntry("Observed class cardinality", choice.Count);
                Console.WriteLine();

                if (iteration != iterationsCount - 1)
                {
                    prior = p.ComputeUpdatedPrior(tempPrefix + "_iteration_" + iteration + "_priorUpdate.psi", inputArity, outputArity, choice, prior);
                    Console.WriteLine("Updated prior: {0}", prior);
                }

                Console.WriteLine();
                Console.WriteLine();
            }
            
            p.WriteLogEntry("Total time", totalTime.Elapsed.TotalSeconds);
        }
    }
}
