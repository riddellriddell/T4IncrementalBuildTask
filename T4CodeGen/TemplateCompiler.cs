using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Mono.TextTemplating;

namespace T4BuildTools
{
    //result of a TemplateCompiler.Compile run: whether the whole run succeeded and
    //the per-template failure messages (mirrors the task's Log.LogError text)
    public class TemplateCompilerResult
    {
        public bool Success { get; internal set; }

        public List<string> TemplateFailures { get; } = new List<string>();
    }

    //standalone, MSBuild-independent T4 incremental generation pipeline. Hosted by
    //the MSBuild task, the CLI exe, or any other front-end; diagnostics flow through
    //the Action<string> log sink and failures through the returned result object.
    public class TemplateCompiler
    {
        public static TemplateCompilerResult Compile(
            string name,
            IList<string> inputFiles,
            IList<string> t4Templates,
            IList<string> generatedFiles,
            string baseIntermediateOutputPath,
            string defaultFileOutputPath,
            Action<string> log)
        {
            TemplateCompilerResult result = new TemplateCompilerResult();

            DateTime mostRecentBuildTime = DateTime.MinValue;

            //create write address
            string allFilesManifestPath = baseIntermediateOutputPath + "GlobalFileManifest.T4Manifest";

            //ensure the intermediate folder exists before reading/writing build state (first builds
            //may roll in before MSBuild has created obj/)
            Directory.CreateDirectory(baseIntermediateOutputPath);

            //log the intermediate and default outputfolders
            log($"Starting incremental build for {name} with intermediate output path {baseIntermediateOutputPath} and default file output path {defaultFileOutputPath}");

            //check if the file exists
            if (File.Exists(allFilesManifestPath))
            {
                mostRecentBuildTime = File.GetLastWriteTime(allFilesManifestPath);
                log($"last build time detected as {mostRecentBuildTime} from file {allFilesManifestPath}");
            }
            else
            {
                log($"File {allFilesManifestPath} does not exist, defaulting to a last build time of {mostRecentBuildTime}");
            }

            {
                //log all the t4 files to process
                log("List of T4 Files");
                for (int i = 0; i < t4Templates.Count; i++)
                {
                    log($"T4 templates to create code with {t4Templates[i]}");
                }

                //log all the input files
                log("List of Input Files");
                for (int i = 0; i < inputFiles.Count; i++)
                {
                    log($"Source files to scan as inputs {inputFiles[i]}");
                }
            }

            //list of dirty files
            List<string> dirtyInputFiles = new List<string>();

            //loop through all the source files
            for (int i = 0; i < inputFiles.Count; i++)
            {
                //get the source file path
                string inputFilePath = inputFiles[i];

                //get time of last change
                DateTime lastChangeTime = File.GetLastWriteTime(inputFilePath);

                //check if file has changed
                if (lastChangeTime > mostRecentBuildTime)
                {
                    //log to console so we can track which files have changed
                    log($"input file {inputFilePath} has changed at {lastChangeTime} which as after the last build at: {mostRecentBuildTime}");

                    //add to dirty source file list
                    dirtyInputFiles.Add(inputFilePath);
                }
            }

            //for each generator file create a list of new files
            Dictionary<string, HashSet<string>> newFilesForGenerator = new Dictionary<string, HashSet<string>>();

            //list of all the dirty template files
            List<string> dirtyTemplateFiles = new List<string>();

            log($"Checking for dirty templates with {t4Templates.Count} templates");

            //loop through all template files
            for (int i = 0; i < t4Templates.Count; i++)
            {
                //get the source file path
                string templateFilePath = t4Templates[i];

                //add to dictionary of all t4 files
                newFilesForGenerator.Add(templateFilePath, new HashSet<string>());

                //get the last change time for the file
                DateTime timeOfLastChange = File.GetLastWriteTime(templateFilePath);

                //check if the file has changed since most recent build
                if (timeOfLastChange > mostRecentBuildTime)
                {
                    log($"Temaplte File {templateFilePath} has changed, adding { inputFiles.Count} files to the list of dirty files");

                    //add template to list of dirty templates
                    dirtyTemplateFiles.Add(templateFilePath);

                    //convert each task list to list of file addresses
                    foreach (string inputFile in inputFiles)
                    {
                        log($"File to generate for {inputFile}");
                        newFilesForGenerator[templateFilePath].Add(inputFile);
                    }
                }
            }

            //add all dirty input files to all templates
            foreach (KeyValuePair<string, HashSet<string>> kvp in newFilesForGenerator)
            {
                HashSet<string> newFiles = kvp.Value;

                //add all the dirty source files
                foreach (string source in dirtyInputFiles)
                {
                    newFiles.Add(source);
                }
            }

            //list of invalid generated files along with their sources
            HashSet<string> invalidGeneratedFiles = new HashSet<string>();

            List<string> dirtyGeneratedFiles = new List<string>();

            //loop through all the generated files
            foreach (string generatedFilePath in generatedFiles)
            {
                //Invalidate against the last recorded build time, not this generated file's own
                //timestamp. The destination copy skips byte-identical writes (so the stamp never
                //advances), which means a generated file whose input was touched after it was last
                //written stays permanently older than that input and would be re-invalidated on
                //every build forever. "Changed since last build" is the authoritative contract: the
                //GlobalFileManifest.T4Manifest is rewritten at the end of every run, so when the
                //scan runs each destination file on disk already reflects its inputs either by
                //regeneration (stamp advanced) or by proven byte-identical content (content current,
                //stamp stale). Only dependencies newer than the last build, or deleted, are stale.
                DateTime referenceTime = mostRecentBuildTime;

                //get the template this source file was generated from
                string templateSerchString = "T4Gen_TemplateFile\\((.*?)\\)";

                //where to store the results
                MatchCollection templateMatches = null;

                //extract inputs from file
                bool didTemplateScanSucceed =
                    FileScanUtility.ScanFileWithRegex(templateSerchString, generatedFilePath, out templateMatches);

                //check if scan worked
                if (!didTemplateScanSucceed)
                {
                    Console.WriteLine($"Failed when scanning file{generatedFilePath}");
                    continue;
                }

                //convert to file list
                List<string> sourceTemplateFiles = FileScanUtility.ConvertMatchListToStringList(templateMatches);

                List<string> changedTemplateFiles = new List<string>();
                List<string> deletedTemplateFiles = new List<string>();

                //check if any source files have changed since this file was created
                bool didTemplateChange = FileScanUtility.ConvertFileListToChangedSinceFileList(referenceTime,
                    sourceTemplateFiles, out changedTemplateFiles, out deletedTemplateFiles);

                //if the template changed then this source file is invalid and needs to be rebuilt or deleted
                if (didTemplateChange)
                {
                    Console.WriteLine($"Template files changed since last build: {string.Join("'", sourceTemplateFiles)}, templates changed since last build: {string.Join("'", changedTemplateFiles)}, templates deleted since last build: {string.Join("'", deletedTemplateFiles)}");

                    //add the file to the list of invalid files
                    invalidGeneratedFiles.Add(generatedFilePath);

                    //no point in going further, the change in template should result in it being rebuilt
                    //with all files as input
                    continue;
                }

                //regex string for finding source paths
                string inputSearchString = "T4Gen_InputFile\\((.*?)\\)";

                //where to store the results
                MatchCollection inputFileRegexMatches = null;

                //extract inputs from file
                bool didSucceed =
                    FileScanUtility.ScanFileWithRegex(inputSearchString, generatedFilePath, out inputFileRegexMatches);

                //check if scan worked
                if (!didSucceed)
                {
                    Console.WriteLine($"Failed when scanning file{generatedFilePath}");
                    continue;
                }

                //build list of all input for this generated file
                List<string> generatedInputFiles = FileScanUtility.ConvertMatchListToStringList(inputFileRegexMatches);

                List<string> changedInputFiles = new List<string>();

                List<string> deletedInputFiles = new List<string>();

                //get list of changed or removed files
                bool didInputsChange = FileScanUtility.ConvertFileListToChangedSinceFileList(referenceTime,
                    generatedInputFiles, out changedInputFiles, out deletedInputFiles);

                //if no inputs changed then we can leave this file as is
                if (!didInputsChange)
                {
                    continue;
                }

                //add file to list of dirty generated files
                dirtyGeneratedFiles.Add(generatedFilePath + $" changed inputs :{string.Join(",", changedInputFiles)} Changed Template Files : {string.Join(",", changedTemplateFiles)}");

                //get list of valid files
                List<string> existingInputFiles = FileScanUtility.ConvertFileListToExistingFileList(generatedInputFiles);

                //because an input changed we need to flag this file as invalid and add all its inputs to the "changed file list"
                //for any valid source files
                invalidGeneratedFiles.Add(generatedFilePath);

                //loop through all source files and add the changed files as inputs
                foreach (string templateFilePath in sourceTemplateFiles)
                {
                    //check if the template file exists in the template file list
                    if (newFilesForGenerator.ContainsKey(templateFilePath))
                    {
                        HashSet<string> changedInputsForGenerator = newFilesForGenerator[templateFilePath];

                        //loop through the changed files and only add them if they have not already beed added
                        foreach (string oldInputFile in existingInputFiles)
                        {
                                changedInputsForGenerator.Add(oldInputFile);
                        }
                    }
                }
            }

            //---------- At this point each generator file should have a list of dirty source files associated with them -----------

            //create one file to hold all the source file addresses
            string allFileAddresses = "";

            //loop through all inputs
            foreach (string inputFilePath in inputFiles)
            {
                allFileAddresses += inputFilePath + Environment.NewLine;
            }

            //overwrite with string
            //update the all files list
            File.WriteAllText(allFilesManifestPath, allFileAddresses);

            //create a temp folder to write all the temp files into
            string tempGeneratedFilesFolder = baseIntermediateOutputPath + "GeneratedFiles";

            //try and create the folder
            try
            {
                Directory.CreateDirectory(tempGeneratedFilesFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                result.Success = false;
                return result;
            }

            //check if there are files already in that directory and if they are then remove them

            List<string> oldGeneratedFiles = Directory.GetFiles(tempGeneratedFilesFolder).ToList();

            log($"Removing existing file in directory {tempGeneratedFilesFolder}");

            //loop through all teh temp files we created and also delete them so they don't get processed in future builds
            foreach (string existingTempGenFile in oldGeneratedFiles)
            {
                log($"Removing existing file {existingTempGenFile} in folder {oldGeneratedFiles}");
                File.Delete(existingTempGenFile);
            }

            //tracks whether any template failed so Compile() can report failure after all templates ran
            bool anyTemplateFailed = false;

            //loop through each template file and execute it
            foreach (var templateFile in newFilesForGenerator)
            {
                //skip files with no changed files
                if (templateFile.Value.Count == 0)
                {
                    log($"Skipping T4 Template {templateFile.Key} because it has no dirty files");
                    continue;
                }

                //print out that the template file is running
                log($"Running T4 Template {templateFile.Key} with dirty files {string.Join(",", templateFile.Value)}");

                //build the changed file manifest for this template file
                string templateFilePath = templateFile.Key.ToString();

                //get the file path without the extension
                string templateName = templateFilePath.Substring(0,
                    templateFilePath.Length - (templateFilePath.Split('.').Last().Length + 1));

                string templateChangedManifestPath = templateName + ".T4ChangedManifest";

                string changedFileText = "";

                foreach (string changedFile in templateFile.Value)
                {
                    changedFileText += changedFile + Environment.NewLine;
                }

                File.WriteAllText(templateChangedManifestPath, changedFileText);

                //snapshot the temp output folder before the run so partial outputs a failing
                //template wrote mid-run can be identified by diffing against this "before" set
                HashSet<string> filesBeforeRun =
                    new HashSet<string>(Directory.GetFiles(tempGeneratedFilesFolder), StringComparer.OrdinalIgnoreCase);

                //run the template in-process with the bundled engine and Roslyn compiler
                string templateErrorText;

                bool didTemplateRun;
                try
                {
                    didTemplateRun = ProcessTemplateInProcess(
                        templateFilePath,
                        tempGeneratedFilesFolder,
                        allFilesManifestPath,
                        templateChangedManifestPath,
                        out templateErrorText);
                }
                catch (Exception e)
                {
                    //an exception escaping the engine host is treated as a template failure
                    templateErrorText = e.ToString();
                    didTemplateRun = false;
                }

                if (didTemplateRun)
                {
                    //log warnings/errors that did not prevent the run from succeeding
                    if (!string.IsNullOrEmpty(templateErrorText))
                    {
                        log($"Errors reported by template {templateFilePath}:{Environment.NewLine}{templateErrorText}");
                    }
                    continue;
                }

                //the template failed: flag it and fail the build, but keep processing the rest
                anyTemplateFailed = true;

                //remove that template's partial outputs (files written into the temp folder mid-run)
                foreach (string partialFile in Directory.GetFiles(tempGeneratedFilesFolder))
                {
                    if (!filesBeforeRun.Contains(partialFile))
                    {
                        log($"Removing partial output {partialFile} left by failed template {templateFilePath}");
                        File.Delete(partialFile);
                    }
                }

                result.TemplateFailures.Add(
                    $"T4 Template {templateFilePath} failed: {(string.IsNullOrEmpty(templateErrorText) ? "no error text reported" : templateErrorText)}");
            }

            //at this point the text gen should have finished and now we need to gather the generated files
            //check if they have a corresponding source file in the existing source file list

            List<string> allFilesInFolder = Directory.GetFiles(tempGeneratedFilesFolder).ToList();

            log($"Found {allFilesInFolder.Count} New files in {tempGeneratedFilesFolder}");

            foreach (string newlyGeneratedFile in allFilesInFolder)
            {
                //log that the file was generated
                log($"Generated file {newlyGeneratedFile} Detected");

                //remove the part of the path for the temp folder and replace it with the
                //actual folder path
                string destinationFilePath = newlyGeneratedFile.Replace(tempGeneratedFilesFolder, defaultFileOutputPath);

                //regex scan string to try and get where this file should be coppied to
                string destScanRegex = "T4Gen_Destination\\((.*?)\\)";

                MatchCollection matches = null;

                //use regex to try and get the destination to copy the file to
                bool didSucceed = FileScanUtility.ScanFileWithRegex(destScanRegex, newlyGeneratedFile, out matches);

                //if the file did define a prefered output dest then extract the folder
                if (!didSucceed)
                {
                    //get the dest list
                    List<string> destinations = FileScanUtility.ConvertMatchListToStringList(matches);

                    if (destinations.Count > 0)
                    {
                        destinationFilePath = destinations.First();
                        log($"Generated file {newlyGeneratedFile} has custom destination {destinationFilePath}");
                    }
                    else
                    {
                        log($"Generated file {newlyGeneratedFile} has malformed destination");
                    }
                }

                //check if it exists in the invalid file list
                if (invalidGeneratedFiles.Contains(destinationFilePath))
                {
                    //remove the file from the invalid list
                    invalidGeneratedFiles.Remove(destinationFilePath);

                    //check if the new file is the same as the old file. we do this to skip copying to prevent
                    //un necessary rebuilds of code
                    if (File.ReadAllText(destinationFilePath).Equals(File.ReadAllText(newlyGeneratedFile)) != true)
                    {
                        log($"Replacing file at {destinationFilePath} With {newlyGeneratedFile}");

                        //copy the new file over the old file
                        File.WriteAllText(destinationFilePath, File.ReadAllText(newlyGeneratedFile));
                    }
                }
                else
                {
                    log($"Copying file to {destinationFilePath} from {newlyGeneratedFile}");

                    //just copy the file over as there is no existing file to compare to
                    File.WriteAllText(destinationFilePath, File.ReadAllText(newlyGeneratedFile));
                }
            }

            //print out list of all dirty files
            log("all dirty templates");

            foreach (string templatefile in dirtyTemplateFiles)
            {
                log($"template :{templatefile}");
            }

            log("all dirty Inputs");

            foreach (string inputFile in dirtyInputFiles)
            {
                log($"header :{inputFile}");
            }

            log("all dirty generated");
            foreach (string generatedFile in dirtyGeneratedFiles)
            {
                log($"Source :{generatedFile}");
            }

            //loop through the remaining invalid files and delete them
            //this is because the sources they were built from changed
            //and no template generated a replacement for them
            foreach (string invalidFile in invalidGeneratedFiles)
            {
                File.Delete(invalidFile);
            }

            //fail the build only if any template failed; all templates were still attempted
            result.Success = !anyTemplateFailed;
            return result;
        }

        //runs a single text template entirely in-process using the bundled Mono.TextTemplating engine
        //and its in-process Roslyn compiler, so no t4.exe and no Visual Studio install is required.
        private static bool ProcessTemplateInProcess(
            string templatePath,
            string outputFolder,
            string globalManifestPath,
            string changedManifestPath,
            out string errorText)
        {
            errorText = "";

            TemplateGenerator generator = new TemplateGenerator();

            //use the bundled in-process Roslyn compiler so no external C# toolchain is needed
            generator.UseInProcessCompiler();

            //flow the template parameters through the host so the <#@ parameter #> directives resolve
            generator.AddParameter(null, null, "OutputFolder", outputFolder);
            generator.AddParameter(null, null, "GlobalFileManifest", globalManifestPath);
            generator.AddParameter(null, null, "ChangeFileManifest", changedManifestPath);

            string templateContent = File.ReadAllText(templatePath);

            //templates flush their own outputs into the temp GeneratedFiles folder, so a null output
            //file name is passed and the returned content is intentionally not written to a second file.
            var result = generator.ProcessTemplateAsync(templatePath, templateContent, null).GetAwaiter().GetResult();

            bool didSucceed = result.success;

            foreach (System.CodeDom.Compiler.CompilerError error in generator.Errors)
            {
                errorText += $"{error.FileName}({error.Line},{error.Column}): " +
                             (error.IsWarning ? "warning" : "error") + $" {error.ErrorNumber}: {error.ErrorText}" +
                             Environment.NewLine;
            }

            return didSucceed;
        }
    }
}