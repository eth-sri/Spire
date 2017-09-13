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

namespace LocationGenerator
{
    struct Rectangle
    {
        public int MinX { get; set; }
        public int MaxX { get; set; }
        public int MinY { get; set; }
        public int MaxY { get; set; }
    }

    class Location
    {
        static void Main(string[] args)
        {
            int width = 0;
            int height = 0;

            int regionsCount = 0;

            string file = "";
            string program = null;
            bool showHelp = false;
            double lowerBound = 0.1;
            double upperBound = 0.9;

            var options = new OptionSet()
            {
                { "width=", "the width of the grid", (int i) => width = i },
                { "height=", "the width of the grid", (int i) => height = i },
                { "out|output-prefix=", "the prefix used to generate output file names", (string s) => file = s },
                { "program=", "the program: identity, constant, random, random-smart", (string s) => program = s },
                { "regions=", "the number of regions for policy", (int i) => regionsCount = i },
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

            if (program != "identity" && program != "constant" && program != "random" && program != "random-smart")
            {
                Console.WriteLine("Program must be one of identity, constant, random, deny.");
                return;
            }

            Random random = new Random();
            
            using (StreamWriter sw = new StreamWriter(file + "prior.psi"))
            {
                sw.WriteLine("def prior()");
                sw.WriteLine("{");
                sw.WriteLine("\tx := uniformInt(0,{0});", width);
                sw.WriteLine("\ty := uniformInt(0,{0});", height);
                sw.WriteLine("\treturn (x, y);");
                sw.WriteLine("}");
            }

            int boxWidth = 2;
            int boxHeight = 2;
            List<Rectangle> hospitals = new List<Rectangle>();
            for (int i = 0; i < regionsCount; i++)
            {
                int x = random.Next(width - boxWidth - 1) + 1;
                int y = random.Next(height - boxHeight - 1) + 1;
                hospitals.Add(new Rectangle() { MinX = x, MinY = y, MaxX = x + boxWidth, MaxY = y + boxHeight });
            }

            char[,] graph = new char[width + 1, height + 1];
            for (int x = 0; x <= width; x++)
            {
                for (int y = 0; y <= height; y++)
                {
                    graph[x, y] = '.';
                }
            }
            foreach (Rectangle rect in hospitals)
            {
                for (int x = rect.MinX; x <= rect.MaxX; x++)
                {
                    for (int y = rect.MinY; y <= rect.MaxY; y++)
                    {
                        graph[x, y] = 'H';
                    }
                }
            }

            using (StreamWriter sw = new StreamWriter(file + "program.psi"))
            {
                sw.WriteLine("def program(x, y)");
                sw.WriteLine("{");

                if (program == "identity")
                {
                    sw.WriteLine("\treturn (x, y);");
                }

                if (program == "constant" || program == "random" || program == "random-smart")
                {
                    string condition = string.Join(" || ", hospitals.Select(h => $"((x >= {h.MinX}) && (x <= {h.MaxX}) && (y >= {h.MinY}) && (y <= {h.MaxY}))"));
                    if (hospitals.Count == 1)
                    {
                        sw.WriteLine("\tif {0}", condition);
                    }
                    else
                    {
                        sw.WriteLine("\tif ({0})", condition);
                    }
                    sw.WriteLine("\t{");
                    if (program == "constant")
                    {
                        sw.WriteLine("\t\treturn (0, 0);");
                    }
                    else if (program == "random")
                    {
                        sw.WriteLine("\t\treturn (uniformInt(0,{0}), uniformInt(0,{1}));", width, height);
                    }
                    else
                    {
                        int outsideCount = 0;
                        for (int x = 0; x <= width; x++)
                        {
                            for (int y = 0; y <= height; y++)
                            {
                                if (graph[x,y] == '.')
                                {
                                    outsideCount++;
                                }
                            }
                        }
                        sw.WriteLine("\t\ttmp := uniformInt(1, {0});", outsideCount);
                        int i = 1;
                        for (int x = 0; x <= width; x++)
                        {
                            for (int y = 0; y <= height; y++)
                            {
                                if (graph[x, y] == '.')
                                {
                                    sw.WriteLine("\t\tif (tmp == {0})", i);
                                    sw.WriteLine("\t\t{");
                                    sw.WriteLine("\t\t\treturn ({0}, {1});", x, y);
                                    sw.WriteLine("\t\t}");
                                    i++;
                                }
                            }
                        }
                    }
                    sw.WriteLine("\t}");
                    sw.WriteLine("\telse");
                    sw.WriteLine("\t{");
                    sw.WriteLine("\t\treturn (x, y);");
                    sw.WriteLine("\t}");
                }

                sw.WriteLine("}");
            }

            using (StreamWriter sw = new StreamWriter(file + "policy.psi"))
            {
                foreach (Rectangle hospital in hospitals)
                {
                    sw.WriteLine($"(input0 >= {hospital.MinX}) && (input0 <= {hospital.MaxX}) && (input1 >= {hospital.MinY}) && (input1 <= {hospital.MaxY}); {lowerBound}; {upperBound}");
                }
            }

            using (StreamWriter sw = new StreamWriter(file + "prior.graph"))
            {
                for (int x = 0; x <= width; x++)
                {
                    for (int y = 0; y <= height; y++)
                    {
                        sw.Write(graph[x, y]);
                    }
                    sw.WriteLine();
                }
            }
        }
    }
}
