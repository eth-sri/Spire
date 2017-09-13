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

using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SocialGenerator
{
    class Social
    {
        static void Main(string[] args)
        {
            int nodeCount = 0;
            int policyCount = 0;
            string file = "";
            string program = null;
            string people = null;
            bool showHelp = false;
            double lowerBound = 0.1;
            double upperBound = 0.9;

            var options = new OptionSet()
            {
                { "nodes=", "the number of nodes in the social graph", (int i) => nodeCount = i },
                { "policy=", "the size of the policy", (int i) => policyCount = i },
                { "out|output-prefix=", "the prefix used to generate output file names", (string s) => file = s },
                { "program=", "the program: sum, noise, subset", (string s) => program = s },
                { "people=", "works with --program=subset, gives the set of people to output the affiliation for in the program", (string s) => people = s },
                { "lower-bound=", "the lower bound for the policy (default value=0.1)", (double d) => lowerBound = d },
                { "upper-bound=", "the upper bound for the policy (default value=0.9)", (double d) => upperBound = d },
                { "help", "shows this help message", v => showHelp = v != null }
            };

            List<string> others = options.Parse(args);

            if (showHelp)
            {
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (others.Count > 0)
            {
                Console.WriteLine("Unknown arguments: {0}", string.Join(" ", others));
                return;
            }

            if (program != "sum" && program != "noise" && program != "subset")
            {
                Console.WriteLine("Program parameter not specified or has wrong value! Allowed values: sum, noise, subset.");
                return;
            }

            if ((people == null && program == "subset") || (people != null && program != "subset"))
            {
                Console.WriteLine("Parameter --nucl-patients-count works only with --program=nucl.");
                return;
            }

            Random random = new Random();

            string tuple = string.Join(", ", Enumerable.Range(0, nodeCount).Select(i => "affiliation_" + i.ToString()));

            using (StreamWriter sw = new StreamWriter(file + "prior.psi"))
            {
                bool[,] friends = new bool[nodeCount, nodeCount];
                for (int i = 0; i < nodeCount; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        friends[i, j] = friends[j, i] = random.Next(2) == 0;
                    }
                }

                sw.WriteLine("def prior()");
                sw.WriteLine("{");
                for (int i = 0; i < nodeCount; i++)
                {
                    sw.WriteLine("\taffiliation_{0} := flip(1/2);", i);
                }
                for (int i = 0; i < nodeCount; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (friends[i, j])
                        {
                            sw.WriteLine("\tif(flip(1/2) == 0)");
                            sw.WriteLine("\t{");
                            sw.WriteLine("\t\tobserve(affiliation_{0} == affiliation_{1});", i, j);
                            sw.WriteLine("\t}");
                        }
                    }
                }
                sw.WriteLine("\treturn ({0});", tuple);
                sw.WriteLine("}");
            }

            using (StreamWriter sw = new StreamWriter(file + "program.psi"))
            {
                sw.WriteLine("def program({0})", tuple);
                sw.WriteLine("{");

                if (program == "sum" || program == "noise")
                {
                    sw.WriteLine("\tsum := 0;");
                    for (int i = 0; i < nodeCount; i++)
                    {
                        sw.WriteLine("\tsum += affiliation_{0};", i);
                    }

                    if (program == "noise")
                    {
                        sw.WriteLine("\tif(flip(1/2) == 0 && sum < " + nodeCount + ")");
                        sw.WriteLine("\t{");
                        sw.WriteLine("\t\tsum += 1;");
                        sw.WriteLine("\t}");
                        sw.WriteLine("\tif(flip(1/2) == 0 && sum > " + 0 + ")");
                        sw.WriteLine("\t{");
                        sw.WriteLine("\t\tsum -= 1;");
                        sw.WriteLine("\t}");
                    }

                    sw.WriteLine("\treturn sum;");
                }

                if (program == "subset")
                {
                    var split = people.Split(',');
                    sw.WriteLine("\treturn ({0});", string.Join(", ", split.Select(p => string.Format("affiliation_{0}, affiliation_{0}", p))));
                }

                sw.WriteLine("}");
            }

            using (StreamWriter sw = new StreamWriter(file + "policy.psi"))
            {
                for (int i = 0; i < policyCount; i++)
                {
                    sw.WriteLine("input{0} == 1; {1}; {2}", i, lowerBound, upperBound);
                }
            }
        }
    }
}
