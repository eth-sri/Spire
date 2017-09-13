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
using Mono.Options;
using System.IO;
using System.Globalization;
using System.Threading;

namespace MedicalGenerator
{
    class Node
    {
        public double? Nucleotide1 { get; set; }

        public double? Nucleotide2 { get; set; }
    }

    class Medical
    {
        static void DFS(StreamWriter sw, Node[] nodes, int current)
        {
            if (current * 2 + 1 < nodes.Length)
            {
                DFS(sw, nodes, current * 2 + 1);
            }
            if (current * 2 + 2 < nodes.Length)
            {
                DFS(sw, nodes, current * 2 + 2);
            }

            if (current * 2 + 1 < nodes.Length)
            {
                sw.WriteLine("\tnode_{0}_nucl_1 := 0;", current);
                sw.WriteLine("\tif (flip(1/2) == 0)");
                sw.WriteLine("\t{");
                sw.WriteLine("\t\tnode_{0}_nucl_1 = node_{1}_nucl_1;", current, current * 2 + 1);
                sw.WriteLine("\t}");
                sw.WriteLine("\telse");
                sw.WriteLine("\t{");
                sw.WriteLine("\t\tnode_{0}_nucl_1 = node_{1}_nucl_2;", current, current * 2 + 1);
                sw.WriteLine("\t}");
            }
            else
            {
                sw.WriteLine("\tnode_{0}_nucl_1 := flip({1});", current, nodes[current].Nucleotide1.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (current * 2 + 2 < nodes.Length)
            {
                sw.WriteLine("\tnode_{0}_nucl_2 := 0;", current);
                sw.WriteLine("\tif (flip(1/2) == 0)");
                sw.WriteLine("\t{");
                sw.WriteLine("\t\tnode_{0}_nucl_2 = node_{1}_nucl_1;", current, current * 2 + 2);
                sw.WriteLine("\t}");
                sw.WriteLine("\telse");
                sw.WriteLine("\t{");
                sw.WriteLine("\t\tnode_{0}_nucl_2 = node_{1}_nucl_2;", current, current * 2 + 2);
                sw.WriteLine("\t}");
            }
            else
            {
                sw.WriteLine("\tnode_{0}_nucl_2 := flip({1});", current, nodes[current].Nucleotide2.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            int nodeCount = 0;
            int policyCount = 0;
            string file = "";
            string program = "";
            string nuclPatients = null;
            bool showHelp = false;
            double lowerBound = 0.1;
            double upperBound = 0.9;

            var options = new OptionSet()
            {
                { "nodes=", "the number of nodes in the complete binary tree", (int i) => nodeCount = i },
                { "policy=", "the size of the policy", (int i) => policyCount = i },
                { "out|output-prefix=", "the prefix used to generate output file names", (string s) => file = s },
                { "program=", "the program: sum, noise, prevalence, nucl", (string s) => program = s },
                { "nucl-patients=", "works together with --program=nucl, determines the patients to output nucleotides for", (string s) => nuclPatients = s },
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

            if (program != "sum" && program != "noise" && program != "prevalence" && program != "nucl")
            {
                Console.WriteLine("Program parameter not specified or has wrong value! Allowed values: sum, noise, prevalence, nucl.");
                return;
            }

            if ((nuclPatients == null && program == "nucl") || (nuclPatients != null && program != "nucl"))
            {
                Console.WriteLine("Parameter --nucl-patients-count works only with --program=nucl.");
                return;
            }

            Node[] nodes = new Node[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i] = new Node();

                if (i * 2 + 1 >= nodeCount)
                {
                    nodes[i].Nucleotide1 = 0.5;
                }

                if (i * 2 + 2 >= nodeCount)
                {
                    nodes[i].Nucleotide2 = 0.5;
                }
            }

            string @params = string.Join(", ", Enumerable.Range(0, nodeCount).Select(i => string.Format("node_{0}_nucl_1, node_{0}_nucl_2", i)));

            using (var sw = new StreamWriter(file + "prior.psi"))
            {
                sw.WriteLine("def prior()");
                sw.WriteLine("{");
                DFS(sw, nodes, 0);
                sw.WriteLine("\treturn ({0});", @params);
                sw.WriteLine("}");
            }

            using (var sw = new StreamWriter(file + "program.psi"))
            {
                sw.WriteLine("def program({0})", @params);
                sw.WriteLine("{");

                if (program == "sum" || program == "noise")
                {
                    sw.WriteLine("\tsum := 0;");
                    for (int i = 0; i < nodeCount; i++)
                    {
                        sw.WriteLine("\tsum += node_{0}_nucl_1;", i);
                        sw.WriteLine("\tsum += node_{0}_nucl_2;", i);
                    }

                    if (program == "noise")
                    {
                        sw.WriteLine("\tif(flip(1/2) == 0 && sum < " + nodeCount * 2 + ")");
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

                if (program == "prevalence")
                {
                    sw.WriteLine("\tprevalence := 0;");
                    for (int i = 0; i < nodeCount; i++)
                    {
                        sw.WriteLine("\tprevalence += (node_{0}_nucl_1 == 1) && (node_{0}_nucl_2 == 1);", i);
                    }
                    sw.WriteLine("\treturn prevalence;");
                }

                if (program == "nucl")
                {
                    var patients = nuclPatients.Split(',');
                    sw.WriteLine("\treturn ({0});", string.Join(", ", patients.Select(p => string.Format("node_{0}_nucl_1, node_{0}_nucl_2", p))));
                }

                sw.WriteLine("}");
            }

            using (var sw = new StreamWriter(file + "policy.psi"))
            {
                var lines = Enumerable.Range(0, policyCount).Select(i => $"(input{i * 2} == 1 && input{i * 2 + 1} == 1)");
                foreach (string s in lines)
                {
                    sw.WriteLine("{0}; {1}; {2}", s, lowerBound, upperBound);
                }
            }
        }
    }
}
