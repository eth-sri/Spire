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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spire
{
    public class EquivalenceRelationClasses
    {
        public List<List<int[]>> Classes { get; set; }

        public string ToString(Dictionary<int[], Rational> outputProbability, List<SecurityAssertion> policy)
        {
            int dimension = outputProbability.Keys.First().Length;

            List<string> classStrings = new List<string>();
            for (int i = 0; i < Classes.Count; i++)
            {
                List<int[]> @class = Classes[i];
                classStrings.Add("{" + string.Join(", ", @class.Select((int[] output) => (dimension == 1 ? "" : "(") + string.Join(",", Array.ConvertAll(output, (int k) => k.ToString())) + (dimension == 1 ? "" : ")"))) + "}");
            }
            int maxLength = classStrings.Max((string s) => s.Length);
            for (int i = 0; i < Classes.Count; i++)
            {
                classStrings[i] = classStrings[i].PadRight(maxLength + 4);
            }

            Rational[,] secretProbs = new Rational[Classes.Count, policy.Count];
            string[,] secretProbsStrings = new string[Classes.Count, policy.Count];
            for (int j = 0; j < policy.Count; j++)
            {
                for (int i = 0; i < Classes.Count; i++)
                {
                    Rational numerator = new Rational(0, 1);
                    Rational denominator = new Rational(0, 1);

                    foreach (int[] output in Classes[i])
                    {
                        numerator += outputProbability[output] * policy[j].SecretProbabilitiesGivenOutput[output];
                        denominator += outputProbability[output];
                    }

                    secretProbs[i, j] = numerator / denominator;
                    secretProbsStrings[i, j] = secretProbs[i, j].ToString();
                }
            }

            int[] maxProbLengths = new int[policy.Count];
            for (int j = 0; j < policy.Count; j++)
            {
                int max = 0;
                for (int i = 0; i < Classes.Count; i++)
                {
                    if (secretProbsStrings[i, j].Length > max)
                    {
                        max = secretProbsStrings[i, j].Length;
                    }
                }
                maxProbLengths[j] = max;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Classes.Count; i++)
            {
                sb.Append(classStrings[i]);
                for (int j = 0; j < policy.Count; j++)
                {
                    sb.AppendFormat("{0} = {1}    ", secretProbs[i, j].ToString().PadRight(maxProbLengths[j]), secretProbs[i, j].ToDouble().ToString("0.00"));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
