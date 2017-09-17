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
using System.IO;
using Mono.Options;
using System.Collections.Generic;
using System.Threading;
using System.Text;

namespace Spire
{
    class MainClass
    {
        static bool CheckFile(string path, string label)
        {
            if (path == null)
            {
                Console.WriteLine("Missing argument: {0}", label);
                return false;
            }
            if (!File.Exists(path))
            {
                Console.WriteLine("File for \"{0}\" at location \"{1}\" does not exist", label, path);
                return false;
            }
            return true;
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Console.OutputEncoding = Encoding.UTF8; // may output 2 whitespace characters

            bool showHelp = false;

            string psiPath = null;
            string priorPath = null;
            string programPath = null;
            string policyPath = null;
            string tempPrefix = null;
            int iterations = 1;
            string particularInput = null;
            string logPath = null;
            string csvPath = null;
            string iterationLogPath = null;
            int psiTimeout = int.MaxValue;
            int z3Timeout = -1;
            string optimizationGoal = null;
            string smtLibFile = null;
            bool iterateWithHeur = false;

            var options = new OptionSet()
            {
                { "psi-path=", "the path to the psi exexutable", (string s) => psiPath = s },
                { "prior=", "prior", (string s) => priorPath = s },
                { "program=", "program", (string s) => programPath = s },
                { "policy=", "policy", (string s) => policyPath = s },
                { "tmp-prefix=", "the prefix used with all temporary files", (string s) => tempPrefix = s },
                { "iterations=", "the number of iterations to perform", (int i) => iterations = i },
                { "input=", "input to use for simulating the program in an interactive setting", (string s) => particularInput = s },
                { "log=", "the file to write log into", (string s) => logPath = s },
                { "csv=", "the file to append csv log into", (string s) => csvPath = s },
                { "psitimeout=", "the cululatime timeout for all psi calls", (int i) => psiTimeout = i },
                { "z3timeout=", "the timeout in milliseconds passed to z3", (int i) => z3Timeout = i },
                { "help", "show this help message", v => showHelp = v != null },
                { "opt-goal=", "the optimization goal: classes (maximizes the number of equivalence relation classes) or singletons (maximizes the number of SINGLETON equivalence classes)", (string s) => optimizationGoal = s},
                { "smt-lib-log=", "the file name to log smt lib formulas given to Z3 into", (string s) => smtLibFile = s },
                { "iterate-heur", "use the heuristic for synthesis when iterating", v => iterateWithHeur = (v != null) },
                { "iteration-log=", "the file to write iteration log into", (string s) => iterationLogPath = s },
            };

            List<string> others = options.Parse(args);

            if (others.Count > 0)
            {
                Console.WriteLine("Unknown argument(s): {0}", string.Join(" ", others));
                return;
            }

            if (showHelp)
            {
                Console.WriteLine("Spire.exe [options]");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (!CheckFile(psiPath, "psiPath")) { return; }
            if (!CheckFile(priorPath, "prior")) { return; }
            if (!CheckFile(policyPath, "policy")) { return; }
            if (!CheckFile(programPath, "program")) { return; }
            if (iterations <= 0)
            {
                Console.WriteLine("Iteartions must be greater than zero.");
                return;
            }
            if (iterations > 1 && particularInput == null)
            {
                Console.WriteLine("For iterative setting, --input parameter must be provided.");
            }
            if (tempPrefix == null)
            {
                Console.WriteLine("Temp files prefix not set.");
                return;
            }
            if ((optimizationGoal != "classes") && (optimizationGoal != "singletons"))
            {
                Console.WriteLine("Optimization goal (opt-goal) not set (or has invalid value)! Allowed values are: classes, singletons.");
                Console.WriteLine("Current value: " + optimizationGoal);
                return;
            }

            try
            {
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                    Console.WriteLine("Log already exists, deleting it...");
                    Console.WriteLine();
                }
                if (File.Exists(csvPath))
                {
                    File.Delete(csvPath);
                    Console.WriteLine("Csv already exists, deleting it...");
                    Console.WriteLine();
                }
                if (!string.IsNullOrWhiteSpace(iterationLogPath) && File.Exists(iterationLogPath))
                {
                    File.Delete(iterationLogPath);
                    Console.WriteLine("Iteration log already exists, deleting it...");
                    Console.WriteLine();
                }

                StreamWriter log = (logPath != null ? new StreamWriter(logPath) : null);
                StreamWriter csv = (csvPath != null ? new StreamWriter(csvPath, true) : null);
                StreamWriter iterationLog = (iterationLogPath != null ? new StreamWriter(iterationLogPath) : null);
                try
                {
                    log.AutoFlush = true;
                    csv.AutoFlush = true;

                    log?.WriteLine("{0}: {1}", "Prior path", priorPath);
                    log?.WriteLine("{0}: {1}", "Program path", programPath);
                    log?.WriteLine("{0}: {1}", "Policy path", policyPath);
                    csv?.Write("{0},{1},{2},", priorPath, programPath, policyPath);

                    PolicySynthesis.IteratePolicySynthesis(
                        psiPath: psiPath,
                        priorPath: priorPath,
                        programPath: programPath,
                        policyPath: policyPath,
                        tempPrefix: tempPrefix,
                        iterationsCount: iterations,
                        particularInput: particularInput,
                        log: log,
                        csv: csv,
                        iterationLog: iterationLog,
                        psiTimeout: psiTimeout,
                        z3Timeout: z3Timeout,
                        optimizationGoal: optimizationGoal,
                        smtLibFile: smtLibFile,
                        iterateWithHeur: iterateWithHeur);

                    csv?.WriteLine();
                }
                finally
                {
                    log?.Dispose();
                    csv?.Dispose();
                    iterationLog?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                //Console.WriteLine("{0}: {1}", ex.GetType().ToString(), ex.Message);
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
            }
        }
    }
}
