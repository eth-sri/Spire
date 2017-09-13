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
using System.Threading;

namespace TableTool
{
    public static class Extensions
    {
        public static double? StandardDeviation(this IEnumerable<double?> values)
        {
            int count = values.Count();
            if (count > 1)
            {
                double? avg = values.Average();
                double? sum = values.Sum(d => (d - avg) * (d - avg));
                if (sum.HasValue)
                {
                    return Math.Sqrt(sum.Value / count);
                }
            }
            return null;
        }

        public static T ArgMax<T>(this IEnumerable<T> source, Func<T, int> selector)
        {
            if (Object.ReferenceEquals(null, source))
                throw new ArgumentNullException("source");

            if (Object.ReferenceEquals(null, selector))
                throw new ArgumentNullException("selector");

            T maxValue = default(T);
            int max = 0;
            Boolean assigned = false;

            foreach (T item in source)
            {
                int v = selector(item);

                if ((v > max) || (!assigned))
                {
                    assigned = true;
                    max = v;
                    maxValue = item;
                }
            }

            return maxValue;
        }
    }

    class Line
    {
        public string Prior { get; set; }

        public string Program { get; set; }

        public string Policy { get; set; }

        public int InputArity { get; set; }

        public int OutputArity { get; set; }

        public double PsiOutputs { get; set; }

        public double PsiSecrets { get; set; }

        public double? Z3Time { get; set; }

        public int? Z3Classes { get; set; }

        public double? Z3SingletonsRatio { get; set; }

        public double? Z3ExpectedClassCardinality { get; set; }

        public double? HeuristicTime { get; set; }

        public int? HeuristicClasses { get; set; }

        public double? HeuristicSingletonsRatio { get; set; }

        public double? HeuristicExpectedClassCardinality { get; set; }

        public double TotalTime { get; set; }
    }

    class CsvFile
    {
        public string FileName { get; set; }

        public List<Line> Lines { get; set; }
    }

    class Table
    {
        static double? ToNullableDouble(string s)
        {
            double value;
            if (double.TryParse(s, out value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        static int? ToNullableInt(string s)
        {
            int value;
            if (int.TryParse(s, out value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        static Line ParseLine(string s)
        {
            // [0] Prior path
            // [1] Program path
            // [2] Policy path
            // [3] Input arity
            // [4] Output arity
            // [5] Output probabilities psi call
            // [6] Secret probabilities psi call/calls
            // [7] Z3 time
            // [8] Z3 classes
            // [9] Z3 Singletons ratio
            // [10] Z3 Expected class cardinality
            // [11] Heuristic time
            // [12] Heuristic classes
            // [13] Heuristic Singletons ratio
            // [14] Heuristic Expected class cardinality
            // [15] Total time

            string[] split = s.Split(',');

            if (split.Length < 16)
            {
                Console.WriteLine($"Line of csv has {split.Length} columns (must be at least 16)!");
                return null;
            }

            return new Line()
            {
                Prior = split[0],
                Program = split[1],
                Policy = split[2],
                InputArity = int.Parse(split[3]),
                OutputArity = int.Parse(split[4]),
                PsiOutputs = double.Parse(split[5]),
                PsiSecrets = double.Parse(split[6]),
                Z3Time = ToNullableDouble(split[7]),
                Z3Classes = ToNullableInt(split[8]),
                Z3SingletonsRatio = ToNullableDouble(split[9]),
                Z3ExpectedClassCardinality = ToNullableDouble(split[10]),
                HeuristicTime = ToNullableDouble(split[11]),
                HeuristicClasses = ToNullableInt(split[12]),
                HeuristicSingletonsRatio = ToNullableDouble(split[13]),
                HeuristicExpectedClassCardinality = ToNullableDouble(split[14]),
                TotalTime = double.Parse(split[15]),
            };
        }

        static List<Line> ParseFile(string inputFile)
        {
            return File.ReadAllLines(inputFile)
                .Select((string fileName) => ParseLine(fileName))
                .Where(l => l != null)
                .OrderBy(l => l.Program, new AlphaNumericComparer())
                .GroupBy((Line l) => l.Program)
                .Select(group => new Line()
                {
                    Program = group.First().Program,
                    Policy = group.First().Policy,
                    Prior = group.First().Prior,
                    InputArity = group.First().InputArity,
                    OutputArity = group.First().OutputArity,
                    Z3Time = group.Average(l => l.Z3Time),
                    Z3Classes = group.FirstOrDefault(l => l.Z3Classes != null)?.Z3Classes,
                    Z3SingletonsRatio = group.Average(l => l.Z3SingletonsRatio),
                    Z3ExpectedClassCardinality = group.Average(l => l.Z3ExpectedClassCardinality),
                    HeuristicTime = group.Average(l => l.HeuristicTime),
                    HeuristicClasses = group.FirstOrDefault(l => l.HeuristicClasses != null)?.HeuristicClasses,
                    HeuristicSingletonsRatio = group.Average(l => l.HeuristicSingletonsRatio),
                    HeuristicExpectedClassCardinality = group.Average(l => l.HeuristicExpectedClassCardinality),
                    PsiOutputs = group.Average(l => l.PsiOutputs),
                    PsiSecrets = group.Average(l => l.PsiSecrets),
                    TotalTime = group.Average(l => l.TotalTime),
                })
                .ToList();
        }

        static void WriteStatsLine(StreamWriter sw, string caption, IEnumerable<double?> data)
        {
            sw.WriteLine(caption + ";{0:0.00};{1:0.00};{2:0.00};{3:0.00}", data.Average(), data.Min(), data.Max(), data.StandardDeviation());
        }

        static void WriteStats(StreamWriter sw, string caption, IEnumerable<Line> lines)
        {
            sw.WriteLine(caption + ";AVG;MIN;MAX;STDDEV");
            sw.WriteLine("Percentage of cases where (Z3 classes = Heuristic classes);{0:0.00}", 100 * (double)lines.Count(l => l.Z3Classes == l.HeuristicClasses) / lines.Count(l => l.Z3Classes != null && l.HeuristicClasses != null));
            WriteStatsLine(sw, "Average of Heuristic classes as a percentage of Z3 classes", lines.Select(l => 100 * (double)l.HeuristicClasses / l.Z3Classes));
            WriteStatsLine(sw, "Average percentage of singleton classes (Z3)", lines.Select(l => 100 * l.Z3SingletonsRatio));
            WriteStatsLine(sw, "Average percentage of singleton classes (Heuristic)", lines.Select(l => 100 * l.HeuristicSingletonsRatio));
            WriteStatsLine(sw, "Average of Heuristic singleton classes as a percentage of Z3 singleton classes", lines.Select(l => 100 * (double)l.HeuristicSingletonsRatio / l.Z3SingletonsRatio).Where(d => d != null && !double.IsNaN(d.Value)));
            WriteStatsLine(sw, "Average of expected class cardinality (Z3)", lines.Select(l => l.Z3ExpectedClassCardinality));
            WriteStatsLine(sw, "Average of expected class cardinality (Heuristic)", lines.Select(l => l.HeuristicExpectedClassCardinality));
            sw.WriteLine();
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            bool help = false;
            string prefix = string.Empty;
            var options = new OptionSet()
            {
                { "help", "displays this help message", v => help = v != null },
                { "prefix", "prefix for the output files", (string s) => prefix = s },
            };
            List<string> csvFileNames = options.Parse(args);
            if (help)
            {
                Console.WriteLine("Table tool.exe [files]");
                return;
            }
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = string.Empty;
            }
            else
            {
                prefix += "-";
            }

            List<CsvFile> csvFiles =
                csvFileNames
                    .Select((string fileName) => new CsvFile {
                        FileName = fileName,
                        Lines = ParseFile(fileName)
                    })
                    .ToList();
            
            using (StreamWriter sw = new StreamWriter(prefix + "psi-times.csv", false))
            {
                sw.WriteLine("label;" + string.Join(";", csvFiles.Select(csv => Path.GetFileNameWithoutExtension(csv.FileName))));
                var maxCsv = csvFiles.ArgMax(csv => csv.Lines.Count);
                for (int i = 0; i < maxCsv.Lines.Count; i++)
                {
                    string[] split = maxCsv.Lines[i].Prior.Split('_');
                    string label = "(" + split[1] + "{,}" + split[2] + ")";
                    sw.WriteLine(label + ";" + string.Join(";", csvFiles.Select<CsvFile, string>((CsvFile csv) => {
                        if (csv.Lines.Count > i)
                        {
                            return (csv.Lines[i].PsiOutputs + csv.Lines[i].PsiSecrets).ToString();
                        }
                        else
                        {
                            return "nan";
                        }
                    })));
                }
            }

            using (StreamWriter sw = new StreamWriter(prefix + "synthesis-times.csv", false))
            {
                sw.WriteLine("label;" + string.Join(";", csvFiles.Select(csv => {
                    string file = Path.GetFileNameWithoutExtension(csv.FileName);
                    return file + "-Z3;" + file + "-H";
                })));
                var maxCsv = csvFiles.ArgMax(csv => csv.Lines.Count);
                for (int i = 0; i < maxCsv.Lines.Count; i++)
                {
                    string[] split = maxCsv.Lines[i].Prior.Split('_');
                    string label = "(" + split[1] + "{,}" + split[2] + ")";
                    sw.WriteLine(label + ";" + string.Join(";", csvFiles.Select<CsvFile, string>((CsvFile csv) => {
                        if (csv.Lines.Count > i)
                        {
                            return (csv.Lines[i].Z3Time.HasValue ? csv.Lines[i].Z3Time.ToString() : "nan") + ";" + csv.Lines[i].HeuristicTime;
                        }
                        else
                        {
                            return "nan;nan";
                        }
                    })));
                }
            }

            using (StreamWriter sw = new StreamWriter(prefix + "table.txt", false))
            {
                sw.WriteLine("Conservative approach denies percent (Z3): {0}",
                    100 *
                    (csvFiles.SelectMany(csv => csv.Lines).Count(l => l.Z3SingletonsRatio < 1) /
                    (double)csvFiles.SelectMany(csv => csv.Lines).Count(l => l.Z3SingletonsRatio != null)));
                sw.WriteLine();

                sw.WriteLine("Conservative approach denies percent (Heuristic): {0}",
                    100 *
                    (csvFiles.SelectMany(csv => csv.Lines).Count(l => l.HeuristicSingletonsRatio < 1) /
                    (double)csvFiles.SelectMany(csv => csv.Lines).Count(l => l.HeuristicSingletonsRatio != null)));
                sw.WriteLine();

                WriteStats(sw, "Everything", csvFiles.SelectMany(csv => csv.Lines));
                foreach (var csv in csvFiles)
                {
                    WriteStats(sw, csv.FileName, csv.Lines);
                }
            }
        }
    }
}
