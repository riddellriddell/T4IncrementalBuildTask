using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using T4BuildTools;

namespace T4CodeGen
{
    //Command-line front-end over the standalone TemplateCompiler API. Runs the same
    //incremental T4 pipeline as the MSBuild task (BuildT4TextFiles) and mirrors its
    //success/failure semantics: exit 0 on success, non-zero if any template failed.
    //
    //Run from the project directory with the same relative inputs the task receives,
    //so generated-file manifest/destination markers come out identical.
    public static class Program
    {
        public static int Main(string[] args)
        {
            string[] expandedArgs = ExpandResponseFiles(args);

            if (expandedArgs.Length == 0 || HasHelp(expandedArgs))
            {
                PrintUsage(Console.Out);
                return expandedArgs.Length == 0 ? 2 : 0;
            }

            CliArgs parsed;
            string parseError;
            if (!TryParse(expandedArgs, out parsed, out parseError))
            {
                Console.Error.WriteLine(parseError);
                PrintUsage(Console.Error);
                return 2;
            }

            TemplateCompilerResult result = TemplateCompiler.Compile(
                parsed.Name,
                parsed.InputFiles,
                parsed.T4Templates,
                parsed.GeneratedFiles,
                parsed.BaseIntermediateOutputPath,
                parsed.DefaultFileOutputPath,
                line => Console.WriteLine(line));

            //forward template failures to stderr, matching the task's Log.LogError text
            foreach (string failure in result.TemplateFailures)
            {
                Console.Error.WriteLine(failure);
            }

            return result.Success ? 0 : 1;
        }

        private static bool HasHelp(string[] args)
        {
            return args.Any(a => a == "-h" || a == "-help" || a == "-?" || string.Equals(a, "/?", StringComparison.Ordinal));
        }

        private static void PrintUsage(TextWriter w)
        {
            w.WriteLine("T4CodeGen - command-line front-end for the standalone T4 incremental compiler (CustomBuildTasks).");
            w.WriteLine();
            w.WriteLine("Usage:");
            w.WriteLine("  T4CodeGen -Name <name> -InputFiles <list> -T4Templates <list> -GeneratedFiles <list>");
            w.WriteLine("            -BaseIntermediateOutputPath <path> -DefaultFileOutputPath <path> [@response.rsp]");
            w.WriteLine();
            w.WriteLine("  Lists (<list>) are pipe ('|') or semicolon (';') separated path values.");
            w.WriteLine("  Run from the project directory, passing the same relative inputs the MSBuild task receives.");
            w.WriteLine();
            w.WriteLine("Options:");
            w.WriteLine("  -Name <name>                     build name (e.g. T4IncrementalBuild).");
            w.WriteLine("  -InputFiles <list>               seed source/header files scanned for changes.");
            w.WriteLine("  -T4Templates <list>              the .tt templates to run.");
            w.WriteLine("  -GeneratedFiles <list>           already-generated .t4generated.* outputs (for invalidation).");
            w.WriteLine("  -BaseIntermediateOutputPath <p>   folder for build state + temp GeneratedFiles (manifest lives here).");
            w.WriteLine("  -DefaultFileOutputPath <p>        default folder generated files are copied back to.");
            w.WriteLine("  @response.rsp                    additional args read from a response file (one per line or space separated).");
            w.WriteLine("  -h|-help|-?                      show this help and exit.");
            w.WriteLine();
            w.WriteLine("Exit code: 0 on success, non-zero if any template failed or the command line is invalid.");
        }

        private static string[] ExpandResponseFiles(string[] args)
        {
            List<string> result = new List<string>();
            foreach (string arg in args)
            {
                if (arg.StartsWith("@", StringComparison.Ordinal) && arg.Length > 1)
                {
                    string path = arg.Substring(1);
                    if (File.Exists(path))
                    {
                        foreach (string line in File.ReadAllLines(path))
                        {
                            string trimmed = line.Trim();
                            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                            {
                                continue;
                            }
                            //one argument per line, so flag and value live on their own lines and
                            //paths containing spaces stay intact
                            result.Add(trimmed);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Response file not found: {path}");
                        Environment.Exit(2);
                    }
                }
                else
                {
                    result.Add(arg);
                }
            }
            return result.ToArray();
        }

        private sealed class CliArgs
        {
            public string Name;
            public List<string> InputFiles = new List<string>();
            public List<string> T4Templates = new List<string>();
            public List<string> GeneratedFiles = new List<string>();
            public string BaseIntermediateOutputPath;
            public string DefaultFileOutputPath;
        }

        private static bool TryParse(string[] args, out CliArgs parsed, out string error)
        {
            parsed = new CliArgs();
            error = null;

            for (int i = 0; i < args.Length; i++)
            {
                string flag = args[i];
                string value = (i + 1 < args.Length) ? args[++i] : null;

                switch (flag.ToLowerInvariant())
                {
                    case "-name":
                        parsed.Name = value;
                        break;
                    case "-inputfiles":
                        AppendList(parsed.InputFiles, value);
                        break;
                    case "-t4templates":
                        AppendList(parsed.T4Templates, value);
                        break;
                    case "-generatedfiles":
                        AppendList(parsed.GeneratedFiles, value);
                        break;
                    case "-baseintermediateoutputpath":
                        parsed.BaseIntermediateOutputPath = value;
                        break;
                    case "-defaultfileoutputpath":
                        parsed.DefaultFileOutputPath = value;
                        break;
                    default:
                        error = $"Unknown argument: {flag}";
                        return false;
                }

                if (value == null)
                {
                    error = $"Missing value for argument: {flag}";
                    return false;
                }
            }

            if (string.IsNullOrEmpty(parsed.Name))
            {
                error = "Missing required argument: -Name";
                return false;
            }
            if (parsed.InputFiles.Count == 0)
            {
                error = "Missing required argument: -InputFiles";
                return false;
            }
            if (parsed.T4Templates.Count == 0)
            {
                error = "Missing required argument: -T4Templates";
                return false;
            }
            if (string.IsNullOrEmpty(parsed.BaseIntermediateOutputPath))
            {
                error = "Missing required argument: -BaseIntermediateOutputPath";
                return false;
            }
            if (string.IsNullOrEmpty(parsed.DefaultFileOutputPath))
            {
                error = "Missing required argument: -DefaultFileOutputPath";
                return false;
            }

            return true;
        }

        private static void AppendList(List<string> target, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            string[] parts = value.IndexOf('|') >= 0
                ? value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                : value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0 && !target.Contains(trimmed))
                {
                    target.Add(trimmed);
                }
            }
        }
    }
}
