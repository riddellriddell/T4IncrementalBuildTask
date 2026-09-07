using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace T4CodeGenTests
{
    //Offline black-box test harness for the standalone T4CodeGen.exe CLI front-end.
    //
    //Spawns the built T4CodeGen\bin\Debug\T4CodeGen.exe against an isolated scratch
    //workspace populated from Fixtures\, runs a case battery, prints PASS/FAIL per
    //case, and exits 0 only if every case passes. No NuGet, no test framework, no
    //network - the harness talks to the exe only over the process boundary.
    public static class Program
    {
        public static int Main(string[] args)
        {
            string repoRoot = RepoRoot();
            string exeUnderTest = Path.Combine(repoRoot, "T4CodeGen", "bin", "Debug", "T4CodeGen.exe");
            string fixturesDir = Path.Combine(repoRoot, "T4CodeGenTests", "Fixtures");
            string testBedDir = Path.Combine(repoRoot, "T4IntegrationTestBed");

            if (!File.Exists(exeUnderTest))
            {
                Console.Error.WriteLine("T4CodeGen.exe not found at " + exeUnderTest);
                Console.Error.WriteLine("Build CustomBuildTasks.csproj and T4CodeGen.csproj (Debug) before running the harness.");
                return 1;
            }
            if (!Directory.Exists(fixturesDir))
            {
                Console.Error.WriteLine("Fixtures not found at " + fixturesDir);
                return 1;
            }

            Harness harness = new Harness(exeUnderTest, fixturesDir, testBedDir);

            List<Case> cases = new List<Case>
            {
                new Case("FreshRegeneration", harness.FreshRegeneration),
                new Case("NoOpRerun", harness.NoOpRerun),
                new Case("DirtyInputRegenerates", harness.DirtyInputRegenerates),
                new Case("BrokenTemplateIsolation", harness.BrokenTemplateIsolation),
                new Case("MissingNameExits2", harness.MissingNameExits2),
                new Case("UnknownFlagExits2", harness.UnknownFlagExits2),
                new Case("ListSeparators", harness.ListSeparators),
                new Case("ResponseFile", harness.ResponseFile),
                new Case("HelpExitZero", harness.HelpExitZero),
                new Case("ResponseFileMissingExits2", harness.ResponseFileMissingExits2),
                new Case("ZeroArgsExits2", harness.ZeroArgsExits2),
                new Case("MissingValueExits2", harness.MissingValueExits2),
                new Case("HelpAliasesExitZero", harness.HelpAliasesExitZero),
                new Case("ResponseFileCommentsAndTrimming", harness.ResponseFileCommentsAndTrimming),
                new Case("CaseInsensitiveFlags", harness.CaseInsensitiveFlags),
                new Case("RepeatedFlagAppends", harness.RepeatedFlagAppends),
            };

            int failures = 0;
            foreach (Case c in cases)
            {
                try
                {
                    c.Body();
                    Console.WriteLine("PASS " + c.Name);
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine("FAIL " + c.Name + " (" + e.Message + ")");
                }
            }

            Console.WriteLine();
            Console.WriteLine((cases.Count - failures) + " of " + cases.Count + " passed");
            return failures == 0 ? 0 : 1;
        }

        private static string RepoRoot()
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        }
    }

    internal sealed class Case
    {
        public string Name;
        public Action Body;

        public Case(string name, Action body)
        {
            Name = name;
            Body = body;
        }
    }

    internal sealed class TestCaseException : Exception
    {
        public TestCaseException(string message) : base(message) { }
    }

    internal sealed class ExitInfo
    {
        public int ExitCode;
        public string StdOut;
        public string StdErr;
        public bool TimedOut;
    }

    internal sealed class Harness
    {
        private static readonly string[] GeneratedFileNames =
        {
            "FancyWrite_HeaderExample.t4generated.h",
            "FancyWrite_TestHeader.t4generated.h",
            "Main_TestHeader.t4generated.h",
            "TestTemplate.t4generated.txt",
        };

        private static readonly string[] SeedInputNames = { "FancyWrite.h", "FancyWrite.cpp", "Main.cpp" };

        private readonly string exeUnderTest;
        private readonly string fixturesDir;
        private readonly string testBedDir;

        public Harness(string exeUnderTest, string fixturesDir, string testBedDir)
        {
            this.exeUnderTest = exeUnderTest;
            this.fixturesDir = fixturesDir;
            this.testBedDir = testBedDir;
        }

        private static string StandardArgs(Scratch s, string separator, bool includeBroken)
        {
            List<string> templateInputs = new List<string>
            {
                "T4Templates\\HeaderExample.tt",
                "T4Templates\\TestTemplate.tt",
            };
            if (includeBroken)
            {
                templateInputs.Add("T4Templates\\Broken.tt");
            }

            StringBuilder sb = new StringBuilder();
            AppendFlag(sb, "-Name", "T4IncrementalBuild");
            AppendFlag(sb, "-InputFiles", string.Join(separator, SeedInputNames));
            AppendFlag(sb, "-T4Templates", string.Join(separator, templateInputs));
            //the task passes absolute GeneratedFiles paths (MSBuildProjectDirectory\**\*.t4generated.*);
            //relative names would never match the absolute destinations the copy step computes and the
            //invalid-file cleanup would delete regenerated outputs
            AppendFlag(sb, "-GeneratedFiles", string.Join("|", AbsoluteGeneratedNames(s.Root)));
            AppendFlag(sb, "-BaseIntermediateOutputPath", s.ObjDir);
            AppendFlag(sb, "-DefaultFileOutputPath", s.Root);
            return sb.ToString();
        }

        private static List<string> AbsoluteGeneratedNames(string root)
        {
            List<string> absolute = new List<string>();
            foreach (string name in GeneratedFileNames)
            {
                absolute.Add(Path.Combine(root, name));
            }
            return absolute;
        }

        private static void AppendFlag(StringBuilder sb, string flag, string value)
        {
            sb.Append(" ");
            sb.Append(flag);
            sb.Append(" \"");
            sb.Append(value);
            sb.Append("\"");
        }

        private static string ResponseFileText(Scratch s, bool includeBroken)
        {
            List<string> templateInputs = new List<string>
            {
                "T4Templates\\HeaderExample.tt",
                "T4Templates\\TestTemplate.tt",
            };
            if (includeBroken)
            {
                templateInputs.Add("T4Templates\\Broken.tt");
            }

            //one argument per line - quotes are not needed because each line is
            //already a single argv entry (paths with spaces stay intact)
            StringBuilder sb = new StringBuilder();
            AppendRspLine(sb, "-Name");
            AppendRspLine(sb, "T4IncrementalBuild");
            AppendRspLine(sb, "-InputFiles");
            AppendRspLine(sb, string.Join("|", SeedInputNames));
            AppendRspLine(sb, "-T4Templates");
            AppendRspLine(sb, string.Join("|", templateInputs));
            AppendRspLine(sb, "-GeneratedFiles");
            AppendRspLine(sb, string.Join("|", AbsoluteGeneratedNames(s.Root)));
            AppendRspLine(sb, "-BaseIntermediateOutputPath");
            AppendRspLine(sb, s.ObjDir);
            AppendRspLine(sb, "-DefaultFileOutputPath");
            AppendRspLine(sb, s.Root);
            return sb.ToString();
        }

        private static void AppendRspLine(StringBuilder sb, string line)
        {
            sb.AppendLine(line);
        }

        private static string MessyResponseFileText(Scratch s)
        {
            //the same args as StandardArgs(s, "|", false), but with comment lines,
            //blank lines, and leading/trailing whitespace the response-file grammar
            //must skip or trim
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# T4 incremental build response file");
            sb.AppendLine("#   indented comment lines are comments too");
            sb.AppendLine();
            AppendRspLine(sb, "-Name");
            AppendRspLine(sb, "  T4IncrementalBuild  ");
            sb.AppendLine();
            AppendRspLine(sb, "-InputFiles");
            AppendRspLine(sb, string.Join("|", SeedInputNames));
            sb.AppendLine();
            sb.AppendLine("# the template list on one line");
            AppendRspLine(sb, "-T4Templates");
            AppendRspLine(sb, "  T4Templates\\HeaderExample.tt|T4Templates\\TestTemplate.tt  ");
            AppendRspLine(sb, "-GeneratedFiles");
            AppendRspLine(sb, string.Join("|", AbsoluteGeneratedNames(s.Root)));
            sb.AppendLine();
            AppendRspLine(sb, "-BaseIntermediateOutputPath");
            AppendRspLine(sb, "  " + s.ObjDir + "  ");
            AppendRspLine(sb, "-DefaultFileOutputPath");
            AppendRspLine(sb, s.Root);
            return sb.ToString();
        }

        private static string RepeatedFlagArgs(Scratch s)
        {
            //the same inputs as StandardArgs(s, "|", false) but each list flag is
            //repeated per value, with one duplicate value to exercise de-duplication
            StringBuilder sb = new StringBuilder();
            AppendFlag(sb, "-Name", "T4IncrementalBuild");
            AppendFlag(sb, "-InputFiles", "FancyWrite.h");
            AppendFlag(sb, "-InputFiles", "FancyWrite.h");
            AppendFlag(sb, "-InputFiles", "FancyWrite.cpp|Main.cpp");
            AppendFlag(sb, "-T4Templates", "T4Templates\\HeaderExample.tt");
            AppendFlag(sb, "-T4Templates", "T4Templates\\TestTemplate.tt");
            AppendFlag(sb, "-GeneratedFiles", string.Join("|", AbsoluteGeneratedNames(s.Root)));
            AppendFlag(sb, "-BaseIntermediateOutputPath", s.ObjDir);
            AppendFlag(sb, "-DefaultFileOutputPath", s.Root);
            return sb.ToString();
        }

        //------------------------------------------------------------ cases

        public void FreshRegeneration()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo r = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(r, 0, "fresh regen");

                foreach (string name in GeneratedFileNames)
                {
                    Assert(() => File.Exists(Path.Combine(s.Root, name)),
                        "generated file missing: " + name);
                }

                //byte-parity against the task-produced baseline: the three generated
                //headers embed only CWD-relative markers, so the scratch run must be
                //byte-identical to the checked-in test bed output
                foreach (string name in GeneratedFileNames)
                {
                    if (name.EndsWith(".txt"))
                    {
                        continue;
                    }
                    byte[] mine = File.ReadAllBytes(Path.Combine(s.Root, name));
                    byte[] baseline = File.ReadAllBytes(Path.Combine(testBedDir, name));
                    Assert(() => ArraysEqual(mine, baseline),
                        name + " differs from the test-bed baseline");
                }

                //the summary txt carries absolute obj paths that differ per scratch;
                //normalize those two lines and compare the rest of the content to the
                //checked-in task baseline
                string mineTxt = File.ReadAllText(Path.Combine(s.Root, "TestTemplate.t4generated.txt"));
                string baselineTxt = File.ReadAllText(Path.Combine(testBedDir, "TestTemplate.t4generated.txt"));
                Assert(() => NormalizeTxt(mineTxt).Equals(NormalizeTxt(baselineTxt), StringComparison.Ordinal),
                    "TestTemplate.t4generated.txt content differs from the test-bed baseline");
            }
        }

        public void NoOpRerun()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo first = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(first, 0, "first run");
                Assert(() => !first.StdOut.Contains("has no dirty files"),
                    "first run unexpectedly skipped templates");

                byte[][] before = ReadGeneratedBytes(s);
                ExitInfo second = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(second, 0, "no-op rerun");
                Assert(() => second.StdOut.Contains("has no dirty files"),
                    "no-op rerun did not skip the templates");

                byte[][] after = ReadGeneratedBytes(s);
                for (int i = 0; i < GeneratedFileNames.Length; i++)
                {
                    int idx = i;
                    Assert(() => ArraysEqual(before[idx], after[idx]),
                        GeneratedFileNames[idx] + " changed on a clean no-op rerun");
                }
            }
        }

        public void DirtyInputRegenerates()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo baselineRun = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(baselineRun, 0, "baseline run");

                byte[] mainBefore = File.ReadAllBytes(Path.Combine(s.Root, "Main_TestHeader.t4generated.h"));
                byte[] headerBefore = File.ReadAllBytes(Path.Combine(s.Root, "FancyWrite_HeaderExample.t4generated.h"));
                byte[] txtBefore = File.ReadAllBytes(Path.Combine(s.Root, "TestTemplate.t4generated.txt"));

                //touch one seed: change its content and stamp it >= 2s after the last
                //build so the >/>= dirty comparison cannot be floored by clock granularity
                string cppPath = Path.Combine(s.Root, "FancyWrite.cpp");
                File.AppendAllText(cppPath, Environment.NewLine + "//dirty-regenerate-test" + Environment.NewLine);
                DateTime manifestTime = File.GetLastWriteTime(s.ManifestPath);
                File.SetLastWriteTime(cppPath, manifestTime.AddSeconds(2));

                ExitInfo dirtyRun = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(dirtyRun, 0, "dirty-input rerun");

                string impactedHeader = File.ReadAllText(Path.Combine(s.Root, "FancyWrite_TestHeader.t4generated.h"));
                Assert(() => impactedHeader.Contains("//dirty-regenerate-test"),
                    "FancyWrite_TestHeader.t4generated.h was not regenerated with the dirty input");

                string dirtyTxt = File.ReadAllText(Path.Combine(s.Root, "TestTemplate.t4generated.txt"));
                Assert(() => dirtyTxt.Contains("//FancyWrite.cpp"),
                    "dirty txt does not list the dirty input");
                Assert(() => !dirtyTxt.Contains("//Main.cpp"),
                    "dirty txt still lists the untouched input");

                Assert(() => ArraysEqual(mainBefore, File.ReadAllBytes(Path.Combine(s.Root, "Main_TestHeader.t4generated.h"))),
                    "Main_TestHeader.t4generated.h regenerated although its input was untouched");
                Assert(() => ArraysEqual(headerBefore, File.ReadAllBytes(Path.Combine(s.Root, "FancyWrite_HeaderExample.t4generated.h"))),
                    "FancyWrite_HeaderExample.t4generated.h regenerated although its input was untouched");
                Assert(() => !ArraysEqual(txtBefore, File.ReadAllBytes(Path.Combine(s.Root, "TestTemplate.t4generated.txt"))),
                    "TestTemplate.t4generated.txt did not refresh on the dirty run");
            }
        }

        public void BrokenTemplateIsolation()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo r = RunExe(s.Root, StandardArgs(s, "|", true));
                AssertExit(r, 1, "broken-template run");

                Assert(() => r.StdErr.Contains("Broken.tt"),
                    "stderr does not name the failing template");

                foreach (string name in GeneratedFileNames)
                {
                    Assert(() => File.Exists(Path.Combine(s.Root, name)),
                        "healthy output missing after broken template: " + name);
                }

                string tempFolder = s.ObjDir + "GeneratedFiles";
                if (Directory.Exists(tempFolder))
                {
                    foreach (string partial in Directory.GetFiles(tempFolder))
                    {
                        string leaf = Path.GetFileName(partial);
                        Assert(() => GeneratedFileNames.Contains(leaf, StringComparer.OrdinalIgnoreCase),
                            "unexpected leftover in generated temp folder: " + leaf);
                    }
                }
            }
        }

        public void MissingNameExits2()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                string argsText = "-InputFiles \"" + string.Join("|", SeedInputNames) + "\""
                    + " -T4Templates \"T4Templates\\HeaderExample.tt\""
                    + " -BaseIntermediateOutputPath \"" + s.ObjDir + "\""
                    + " -DefaultFileOutputPath \"" + s.Root + "\"";
                ExitInfo r = RunExe(s.Root, argsText);
                AssertExit(r, 2, "missing -Name");
                Assert(() => r.StdErr.Contains("Missing required argument: -Name"),
                    "stderr does not report the missing -Name");
                Assert(() => r.StdErr.Contains("Usage"),
                    "stderr does not print usage");
            }
        }

        public void UnknownFlagExits2()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo r = RunExe(s.Root, "-bogus value");
                AssertExit(r, 2, "unknown flag");
                Assert(() => r.StdErr.Contains("Unknown argument: -bogus"),
                    "stderr does not report the unknown flag");
                Assert(() => r.StdErr.Contains("Usage"),
                    "stderr does not print usage");
            }
        }

        public void ListSeparators()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo pipe = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(pipe, 0, "pipe-separated run");
                byte[][] pipeBytes = ReadGeneratedBytes(s);

                s.Reset();
                ExitInfo semi = RunExe(s.Root, StandardArgs(s, ";", false));
                AssertExit(semi, 0, "semicolon-separated run");
                byte[][] semiBytes = ReadGeneratedBytes(s);

                for (int i = 0; i < GeneratedFileNames.Length; i++)
                {
                    int idx = i;
                    Assert(() => ArraysEqual(pipeBytes[idx], semiBytes[idx]),
                        GeneratedFileNames[idx] + " differs between | and ; separated lists");
                }
            }
        }

        public void ResponseFile()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo explicitRun = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(explicitRun, 0, "explicit-args run");
                byte[][] explicitBytes = ReadGeneratedBytes(s);

                s.Reset();
                string rspPath = Path.Combine(s.Root, "test.rsp");
                File.WriteAllText(rspPath, ResponseFileText(s, false), Encoding.UTF8);

                ExitInfo rspRun = RunExe(s.Root, "@test.rsp");
                AssertExit(rspRun, 0, "response-file run");
                byte[][] rspBytes = ReadGeneratedBytes(s);

                for (int i = 0; i < GeneratedFileNames.Length; i++)
                {
                    int idx = i;
                    Assert(() => ArraysEqual(explicitBytes[idx], rspBytes[idx]),
                        GeneratedFileNames[idx] + " differs between explicit args and @test.rsp");
                }
            }
        }

        public void HelpExitZero()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo r = RunExe(s.Root, "-h");
                AssertExit(r, 0, "-h");
                Assert(() => r.StdOut.Contains("Usage"),
                    "stdout does not print usage for -h");
            }
        }

        public void ResponseFileMissingExits2()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo r = RunExe(s.Root, "@does-not-exist.rsp");
                AssertExit(r, 2, "missing response file");
                Assert(() => r.StdErr.Contains("Response file not found: does-not-exist.rsp"),
                    "stderr does not report the missing response file");
                Assert(() => !r.StdErr.Contains("Usage"),
                    "missing response file prints usage");
            }
        }

        public void ZeroArgsExits2()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo r = RunExe(s.Root, "");
                AssertExit(r, 2, "zero args");
                Assert(() => r.StdOut.Contains("Usage"),
                    "stdout does not print usage for zero args");
                Assert(() => !r.StdErr.Contains("Usage"),
                    "zero-args usage leaked to stderr");
            }
        }

        public void MissingValueExits2()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo r = RunExe(s.Root, "-InputFiles");
                AssertExit(r, 2, "missing value");
                Assert(() => r.StdErr.Contains("Missing value for argument: -InputFiles"),
                    "stderr does not report the missing value");
                Assert(() => r.StdErr.Contains("Usage"),
                    "stderr does not print usage");
            }
        }

        public void HelpAliasesExitZero()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                foreach (string alias in new[] { "-h", "-help", "-?", "/?" })
                {
                    ExitInfo r = RunExe(s.Root, alias);
                    AssertExit(r, 0, "help alias " + alias);
                    Assert(() => r.StdOut.Contains("Usage"),
                        "stdout does not print usage for " + alias);
                }

                //a help token anywhere on the command line wins over real arguments
                ExitInfo anywhere = RunExe(s.Root, "-Name T4IncrementalBuild -h");
                AssertExit(anywhere, 0, "help anywhere");
                Assert(() => anywhere.StdOut.Contains("Usage"),
                    "stdout does not print usage when -h appears alongside real arguments");
            }
        }

        public void ResponseFileCommentsAndTrimming()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo explicitRun = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(explicitRun, 0, "explicit-args run");
                byte[][] explicitBytes = ReadGeneratedBytes(s);

                s.Reset();
                string rspPath = Path.Combine(s.Root, "test.rsp");
                File.WriteAllText(rspPath, MessyResponseFileText(s), Encoding.UTF8);

                ExitInfo rspRun = RunExe(s.Root, "@test.rsp");
                AssertExit(rspRun, 0, "response-file run");
                Assert(() => string.IsNullOrEmpty(rspRun.StdErr.Trim()),
                    "response-file run wrote to stderr");
                byte[][] rspBytes = ReadGeneratedBytes(s);

                for (int i = 0; i < GeneratedFileNames.Length; i++)
                {
                    int idx = i;
                    Assert(() => ArraysEqual(explicitBytes[idx], rspBytes[idx]),
                        GeneratedFileNames[idx] + " differs when the response file has comments/blank lines/whitespace");
                }
            }
        }

        public void CaseInsensitiveFlags()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo canonical = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(canonical, 0, "canonical-case run");
                Assert(() => string.IsNullOrEmpty(canonical.StdErr.Trim()),
                    "canonical-case run wrote to stderr");
                byte[][] canonicalBytes = ReadGeneratedBytes(s);

                s.Reset();
                string upper = StandardArgs(s, "|", false)
                    .Replace("-Name", "-NAME")
                    .Replace("-InputFiles", "-INPUTFILES")
                    .Replace("-T4Templates", "-T4TEMPLATES")
                    .Replace("-GeneratedFiles", "-GENERATEDFILES")
                    .Replace("-BaseIntermediateOutputPath", "-BASEINTERMEDIATEOUTPUTPATH")
                    .Replace("-DefaultFileOutputPath", "-DEFAULTFILEOUTPUTPATH");
                ExitInfo upperRun = RunExe(s.Root, upper);
                AssertExit(upperRun, 0, "uppercase-case run");
                Assert(() => string.IsNullOrEmpty(upperRun.StdErr.Trim()),
                    "uppercase-case run wrote to stderr");
                byte[][] upperBytes = ReadGeneratedBytes(s);

                for (int i = 0; i < GeneratedFileNames.Length; i++)
                {
                    int idx = i;
                    Assert(() => ArraysEqual(canonicalBytes[idx], upperBytes[idx]),
                        GeneratedFileNames[idx] + " differs between canonical-case and uppercase-case flags");
                }
            }
        }

        public void RepeatedFlagAppends()
        {
            using (Scratch s = new Scratch(fixturesDir))
            {
                ExitInfo combined = RunExe(s.Root, StandardArgs(s, "|", false));
                AssertExit(combined, 0, "combined-list run");
                Assert(() => string.IsNullOrEmpty(combined.StdErr.Trim()),
                    "combined-list run wrote to stderr");
                byte[][] combinedBytes = ReadGeneratedBytes(s);

                s.Reset();
                ExitInfo repeated = RunExe(s.Root, RepeatedFlagArgs(s));
                AssertExit(repeated, 0, "repeated-flag run");
                Assert(() => string.IsNullOrEmpty(repeated.StdErr.Trim()),
                    "repeated-flag run wrote to stderr");
                byte[][] repeatedBytes = ReadGeneratedBytes(s);

                for (int i = 0; i < GeneratedFileNames.Length; i++)
                {
                    int idx = i;
                    Assert(() => ArraysEqual(combinedBytes[idx], repeatedBytes[idx]),
                        GeneratedFileNames[idx] + " differs between combined and repeated flags");
                }
            }
        }

        //------------------------------------------------------------ helpers

        private ExitInfo RunExe(string workDir, string argsText)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exeUnderTest,
                Arguments = argsText,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (Process p = Process.Start(psi))
            {
                StringBuilder stdout = new StringBuilder();
                StringBuilder stderr = new StringBuilder();
                p.OutputDataReceived += (o, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                p.ErrorDataReceived += (o, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                if (!p.WaitForExit(60000))
                {
                    try { p.Kill(); }
                    catch { }
                    return new ExitInfo { TimedOut = true, StdOut = stdout.ToString(), StdErr = stderr.ToString() };
                }

                p.WaitForExit();
                return new ExitInfo { ExitCode = p.ExitCode, StdOut = stdout.ToString(), StdErr = stderr.ToString() };
            }
        }

        private byte[][] ReadGeneratedBytes(Scratch s)
        {
            byte[][] all = new byte[GeneratedFileNames.Length][];
            for (int i = 0; i < GeneratedFileNames.Length; i++)
            {
                all[i] = File.ReadAllBytes(Path.Combine(s.Root, GeneratedFileNames[i]));
            }
            return all;
        }

        private static void AssertExit(ExitInfo r, int expected, string what)
        {
            Assert(() => !r.TimedOut, what + " timed out (process hung)");
            Assert(() => r.ExitCode == expected,
                what + " exited " + r.ExitCode + " but " + expected + " was expected");
        }

        private static void Assert(Func<bool> condition, string message)
        {
            if (!condition())
            {
                throw new TestCaseException(message);
            }
        }

        private static bool ArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static string NormalizeTxt(string content)
        {
            content = content.Replace("\r\n", "\n");
            content = Regex.Replace(content, "^// Output folder: .*$", "// Output folder: {OBJ}", RegexOptions.Multiline);
            content = Regex.Replace(content, "^// Global Manifest : .*$", "// Global Manifest : {OBJ}", RegexOptions.Multiline);
            return content;
        }
    }

    //isolated copy of the fixture set. Mirrors the test bed layout: seeds at the
    //workspace root, templates under T4Templates\ so CWD-relative inputs produce
    //the same generated markers and output file names as the task's.
    internal sealed class Scratch : IDisposable
    {
        private readonly string fixturesDir;

        public string Root { get; }
        public string ObjDir { get; }
        public string ManifestPath { get; }

        public Scratch(string fixturesDir)
        {
            this.fixturesDir = fixturesDir;
            Root = Path.Combine(Path.GetTempPath(), "T4CodeGenTests_" + Guid.NewGuid().ToString("N"));
            //the exe concatenates BaseIntermediateOutputPath onto the manifest and
            //temp generated folder without a separator (objGlobalFileManifest.T4Manifest,
            //objGeneratedFiles), so ObjDir mirrors the task's "...\obj" string exactly
            ObjDir = Root + "obj";
            ManifestPath = Root + "objGlobalFileManifest.T4Manifest";
            Populate();
        }

        private void Populate()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(Root, "T4Templates"));

            foreach (string seed in new[] { "FancyWrite.h", "FancyWrite.cpp", "Main.cpp" })
            {
                CopyFile(Path.Combine(fixturesDir, seed), Path.Combine(Root, seed));
            }
            foreach (string template in new[]
                {
                    "HeaderExample.tt",
                    "TestTemplate.tt",
                    "CodeGenUtilities.ttinclude",
                    "Broken.tt",
                })
            {
                CopyFile(Path.Combine(fixturesDir, "T4Templates", template),
                    Path.Combine(Root, "T4Templates", template));
            }

            //normalize file times so the baseline run starts clean and the >/>= dirty
            //comparisons have a stable floor well behind any future manifest writes
            DateTime baseline = DateTime.Now.AddDays(-2);
            foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetLastWriteTime(file, baseline);
            }
        }

        private static void CopyFile(string source, string destination)
        {
            File.Copy(source, destination, true);
        }

        //removes all incremental build state and outputs so the next run is a full
        //regeneration while keeping the same workplace root (absolute paths stay
        //identical across the compared runs)
        public void Reset()
        {
            if (Directory.Exists(ObjDir))
            {
                Directory.Delete(ObjDir, true);
            }
            if (File.Exists(ManifestPath))
            {
                File.Delete(ManifestPath);
            }

            foreach (string file in Directory.GetFiles(Root, "*.t4generated.*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }
            foreach (string manifest in Directory.GetFiles(Path.Combine(Root, "T4Templates"), "*.T4ChangedManifest"))
            {
                File.Delete(manifest);
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, true);
                }
            }
            catch
            {
            }
        }
    }
}