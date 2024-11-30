using System;
using System.Collections.Generic;

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;


//Custom build task debugging is done by making the "start" action for this project to open up ms build and run the debug project

namespace T4BuildTools
{
    public class TLogParser
    {
        public static Dictionary<string, List<string>> ParseReadTLog(string tlogFilePath)
        {
            var dependencies = new Dictionary<string, List<string>>();
            string currentCppFile = null;

            foreach (var line in File.ReadLines(tlogFilePath))
            {
                var trimmedLine = line.Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                // Detect new .cpp file entry
                if (trimmedLine.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase))
                {
                    currentCppFile = trimmedLine;
                    if (!dependencies.ContainsKey(currentCppFile))
                    {
                        dependencies[currentCppFile] = new List<string>();
                    }
                }
                else if (currentCppFile != null)
                {
                    // Add dependency for the current .cpp file
                    dependencies[currentCppFile].Add(trimmedLine);
                }
            }

            return dependencies;
        }
    }

    public class BuildT4TextFiles : Task
    {
        struct T4RebuildTicket
        {
            //path to this file
            bool BuildNeeded;
            
            //header and source files that include the output of this file
            HashSet<string> DependentFiles;
        }

        struct GeneratedFileBuildInformation
        {
            //the template that created this file
            public string m_sourceTemplate;

            //the files that were used as input for the creation of this file
            public List<string> m_inputFiles;
        }

        [Required]
        public string Name { get; set; }

        [Required]
        public ITaskItem[] TLogReadFiles { get; set; }

        [Required]
        public ITaskItem[] HeaderFiles { get; set; } //all the header files that may need generation

        [Required]
        public ITaskItem[] SourceFiles { get; set; } //all the source files that may need generation

        [Required]
        public ITaskItem[] TargetT4Files { get; set; } //the text template to generate from

        [Required]
        public string BaseIntermediateOutputPath { get; set; } //the temp folder to build into
        
        [Required]
        public string GeneratedFileOutputPath { get; set; } //the desitnation folder for built files
        
        [Required]
        public string ProjectRootFolder { get; set; } //the desitnation folder for built files
        
        [Required]
        public ITaskItem[] GeneratedFiles { get; set; } //the files that have been created by the template file generator 
        
        [Required]
        public ITaskItem[] ObjectFiles { get; set; } //the object files in the temp directory

        public override bool Execute()
        {
            DateTime mostRecentBuildTime = DateTime.MinValue;
            
            Dictionary<string, List<string>> combinedSourceFileDependencies = new Dictionary<string, List<string>>();
            
            //get the list of pdb files created and turn it into a dictionary
            Dictionary<string, DateTime> objFiles = new Dictionary<string, DateTime>();
            
            //create write address
            string allFilesManifestPath = BaseIntermediateOutputPath + "GlobalFileManifest.T4Manifest";

            //check if the file exists
            if (File.Exists(allFilesManifestPath))
            {
                mostRecentBuildTime = File.GetLastWriteTime(allFilesManifestPath);
                Log.LogMessage(MessageImportance.High, $"last build time detected as {mostRecentBuildTime} from file {allFilesManifestPath}");
            }

            {
                Log.LogMessage(MessageImportance.High, "Hello{0} Running build T4 task", Name);

                //log all the t4 files to process
                Log.LogMessage(MessageImportance.High, "List of T4 Files");
                for (int i = 0; i < TargetT4Files.Length; i++)
                {
                    //file
                    Log.LogMessage(MessageImportance.High,
                        $"T4 templates to create code with {TargetT4Files[i].ToString()}");
                }

                //log all the header files
                Log.LogMessage(MessageImportance.High, "List of header files");
                for (int i = 0; i < HeaderFiles.Length; i++)
                {
                    //file
                    Log.LogMessage(MessageImportance.High,
                        $"Header Files to scan as inputs {HeaderFiles[i].ToString()}");
                }

                //log all the source files
                Log.LogMessage(MessageImportance.High, "List of source");
                for (int i = 0; i < SourceFiles.Length; i++)
                {
                    //file
                    Log.LogMessage(MessageImportance.High,
                        $"Source files to scan as inputs {SourceFiles[i].ToString()} with item spec {SourceFiles[i].ItemSpec}");
                }

                /*

                //log all the TLog files
                Log.LogMessage(MessageImportance.High, "List of TLog Read Files");
                for (int i = 0; i < TLogReadFiles.Length; i++)
                {
                    //file
                    Log.LogMessage(MessageImportance.High,
                        $"File to generate for {TLogReadFiles[i].ToString()} with item spec {TLogReadFiles[i].ItemSpec}");

                    //try and parse the Tlog File
                    //try and process the TLog file
                    Dictionary<string, List<string>> sourceFileDependencies =
                        TLogParser.ParseReadTLog(TLogReadFiles[i].ToString());

                    foreach (var kvp in sourceFileDependencies)
                    {
                        if (!combinedSourceFileDependencies.ContainsKey(kvp.Key))
                        {
                            combinedSourceFileDependencies.Add(kvp.Key, kvp.Value);
                        }
                        else
                        {
                            combinedSourceFileDependencies[kvp.Key]
                                .AddRange(kvp.Value); // Update the value for existing keys
                        }
                    }

                    //log the list to string
                    Log.LogMessage(MessageImportance.High,
                        $"ParsedTlogDependencies: {sourceFileDependencies.ToString()}");
                }



                //log all the object files created at last build
                Log.LogMessage(MessageImportance.High, "List of TLog Read Files");
                for (int i = 0; ObjectFiles != null && i < ObjectFiles.Length; i++)
                {
                    //file
                    Log.LogMessage(MessageImportance.High, $"File created at last build {ObjectFiles[i].ToString()}");

                    //try get time of creation
                    DateTime time = File.GetLastWriteTime(ObjectFiles[i].ToString());

                    //check if time is 0
                    if (time == DateTime.MinValue)
                    {
                        continue;
                    }

                    //get the path
                    string objectFilePth = ObjectFiles[i].ToString();

                    string[] splitString = objectFilePth.Split('/', '\\');

                    string fileNameWithExtension = splitString.Last();

                    string fileNameNoExtension = fileNameWithExtension.Replace(".obj", "");

                    //add to list of object files
                    if (objFiles.ContainsKey(fileNameWithExtension) && objFiles[fileNameWithExtension] >= time)
                    {
                        continue;
                    }

                    objFiles[fileNameNoExtension] = time;

                    //update the most recent build time
                    mostRecentBuildTime = mostRecentBuildTime > time ? time : mostRecentBuildTime;
                }
                */
            }

            //list of dirty files
            List<string> dirtySourceFiles = new List<string>();

            List<string> dirtyHeaderFiles = new List<string>();

            //List<string> includedHeaderFiles = new List<string>();

            //loop through all the source files
            for (int i = 0; i < SourceFiles.Length; i++)
            {
                //get the source file path
                string sourceFilePath = SourceFiles[i].ToString();

                //get the source file path without the extension
                string sourceFileWithoutExtenstion = sourceFilePath.Split('/', '\\').Last().Replace(".cpp", "");

                //get time of last change
                DateTime lastChangeTime = File.GetLastWriteTime(sourceFilePath);
                
                //check if file has changed
                if (lastChangeTime > mostRecentBuildTime)
                {
                    //log to console so we can track which files have changed
                    Console.WriteLine($"HeaderFile {sourceFilePath} has changed since the last build at: {mostRecentBuildTime}");
                    
                    //add to dirty source file list
                    dirtySourceFiles.Add(sourceFilePath);
                }
                
                //DateTime lastBuildTime = DateTime.MinValue;

                /*
                //try and get pdb file 
                if (objFiles.ContainsKey(sourceFileWithoutExtenstion))
                {
                    //file has never been compiled, it is dirty
                    lastBuildTime = objFiles[sourceFileWithoutExtenstion];
                    
                    //check if this has changed since las build
                    if (lastBuildTime < lastChangeTime)
                    {
                        //file has changed since last build, flag it for list of dirty files 
                        if (!dirtySourceFiles.Contains(sourceFilePath))
                        {
                            dirtySourceFiles.Add(sourceFilePath);
                        }

                    }
                }
                else
                {
                    //has not been built ever so must be dirty
                    dirtySourceFiles.Add(sourceFilePath);
                }
                
                

                //check if the source file is in the tlog structure
                if (combinedSourceFileDependencies.ContainsKey(sourceFilePath))
                {
                    //loop through all source files and check their last change time
                    List<string> dependencies = combinedSourceFileDependencies[sourceFilePath];

                    foreach (string header in dependencies)
                    {
                        //add to list of tracked header files
                        if (!includedHeaderFiles.Contains(header))
                        {
                            includedHeaderFiles.Add(header);
                        }

                        //get the last change time for the file
                        DateTime headerLastChangeTime = File.GetLastWriteTime(header);

                        if (headerLastChangeTime > lastChangeTime)
                        {
                            //file has changed since last build, flag it for list of dirty files 
                            if (!dirtyHeaderFiles.Contains(header))
                            {
                                dirtyHeaderFiles.Add(header);
                            }
                        }
                    }
                }
                */
            }

            //loop through all the header files and make sure they were included at least once
            for (int i = 0; i < HeaderFiles.Length; i++)
            {
                string filePath = HeaderFiles[i].ToString();

                //get time of last change
                DateTime lastChangeTime = File.GetLastWriteTime(filePath);

                //check if the file is newer than the newest build time
                if (lastChangeTime > mostRecentBuildTime)
                {
                    Console.WriteLine($"HeaderFile {filePath} has changed since the last build at: {mostRecentBuildTime}");
                    
                    //add to list of dirty header files
                    dirtyHeaderFiles.Add(filePath);
                }
            }

            //for each generator file create a list of new files
            Dictionary<string, HashSet<string>> newFilesForGenerator = new Dictionary<string, HashSet<string>>();

            //list of all the dirty template files
            List<string> dirtyTemplateFiles = new List<string>();
            
            //loop through all template files
            for (int i = 0; i < TargetT4Files.Length; i++)
            {
                //get the source file path
                string templateFilePath = TargetT4Files[i].ToString();

                //add to dictionary of all t4 files
                newFilesForGenerator.Add(templateFilePath, new HashSet<string>());

                //get the last change time for the file
                DateTime timeOfLastChange = File.GetLastWriteTime(templateFilePath);

                //check if the file has changed since most recent build 
                if (timeOfLastChange > mostRecentBuildTime)
                {
                    //add template to list of dirty templates
                    dirtyTemplateFiles.Add(templateFilePath);
                    
                    //convert each task list to list of file addresses
                    foreach (ITaskItem item in HeaderFiles)
                    {
                        Log.LogMessage(MessageImportance.High, $"File to generate for {item.ItemSpec}");
                        newFilesForGenerator[templateFilePath].Add(item.ToString());
                    }

                    foreach (ITaskItem item in SourceFiles)
                    {
                        Log.LogMessage(MessageImportance.High, $"File to generate for {item.ItemSpec}");
                        newFilesForGenerator[templateFilePath].Add(item.ToString());
                    }
                }

            }
            
            //add all dirty source and header files to all templates
            foreach (KeyValuePair<string, HashSet<string>> kvp in newFilesForGenerator)
            {
                HashSet<string> newFiles = kvp.Value;

                //add all the dirty source files
                foreach (string source in dirtySourceFiles)
                {
                    newFiles.Add(source);
                }
                
                //add all the dirty header files
                foreach (string header in dirtyHeaderFiles)
                {
                    newFiles.Add(header);
                }
            }

            //list of invalid generated files along with their sources
            HashSet<string> invalidGeneratedFiles = new HashSet<string>();
            
            List<string> dirtyGeneratedFiles = new List<string>();

            //loop through all the generated files 
            foreach (var file in GeneratedFiles)
            {
                //get the path to the generated file
                string generatedFilePath = file.ToString();

                //get the last write time for the file
                DateTime timeOfLastChange = File.GetLastWriteTime(generatedFilePath);

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
                List<string> sourceTemplateFiles = FileScanUtility.ConvertMatchListToFileList(templateMatches);

                List<string> changedTemplateFiles = new List<string>();
                List<string> deletedTemplateFiles = new List<string>();

                //check if any source files have changed since this file was created
                bool didTemplateChange = FileScanUtility.ConvertFileListToChangedSinceFileList(timeOfLastChange,
                    sourceTemplateFiles, out changedTemplateFiles, out deletedTemplateFiles);

                //if the template changed then this source file is invalid and needs to be rebuilt or deleted 
                if (didTemplateChange)
                {
                    Console.WriteLine($"Template files changed since last build{string.Join("'", sourceTemplateFiles)}, templates changed since last build{string.Join("'", changedTemplateFiles)}, templates deleted since last build{string.Join("'", deletedTemplateFiles)}");
                    
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
                List<string> inputFiles = FileScanUtility.ConvertMatchListToFileList(inputFileRegexMatches);

                List<string> changedInputFiles = new List<string>();

                List<string> deletedInputFiles = new List<string>();

                //get list of changed or removed files
                bool didInputsChange = FileScanUtility.ConvertFileListToChangedSinceFileList(timeOfLastChange,
                    inputFiles, out changedInputFiles, out deletedInputFiles);

                //if no inputs changed then we can leave this file as is
                if (!didInputsChange)
                {
                    continue;
                }
                
                //add file to list of dirty generated files
                dirtyGeneratedFiles.Add(generatedFilePath + $" changed inputs :{string.Join(",", changedInputFiles)} Changed Template Files : {string.Join(",", changedTemplateFiles)}");

                //get list of valid files
                List<string> existingInputFiles = FileScanUtility.ConvertFileListToExistingFileList(inputFiles);

                //because an input changed we need to flag this file as invalid and add all its inputs to the "changed file list"
                //for any valid source files
                invalidGeneratedFiles.Add(generatedFilePath);

                //loop through all source fills and add the changed files as inputs
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

            //loop through both sets of inputs
            foreach (ITaskItem header in HeaderFiles)
            {
                allFileAddresses += header.ToString() + Environment.NewLine;
            }

            foreach (ITaskItem source in SourceFiles)
            {
                allFileAddresses += source.ToString() + Environment.NewLine;
            }

            //overwrite with string
            //update the all files list
            File.WriteAllText(allFilesManifestPath, allFileAddresses);

            //create a temp folder to write all the temp files into
            string tempGeneratedFilesFolder = BaseIntermediateOutputPath + "GeneratedFiles";

            //try and create the folder 
            try
            {
                Directory.CreateDirectory(tempGeneratedFilesFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
            
            //check if there are files already in that directory and if they are then remove them
            
            List<string> oldGeneratedFiles = Directory.GetFiles(tempGeneratedFilesFolder).ToList();
            
            //loop through all teh temp files we created and also delete them so they don't get processed in future builds
            foreach (string existingTempGenFile in oldGeneratedFiles)
            {
                File.Delete(existingTempGenFile);
            }

            //list of all the active processes 
            List<Process> activeProcesses = new List<Process>();

            //loop through each template file and execute it
            foreach (var templateFile in newFilesForGenerator)
            {
                //skip files with no changed files
                if (templateFile.Value.Count == 0)
                {
                    continue;
                }
                
                //print out that the template file is running 
                Console.WriteLine($"Running T4 Template {templateFile.Key} with dirty files {string.Join(",", templateFile.Value)}");

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

                //build template command
                string templateCommand = "t4 -p=OutputFolder='" + tempGeneratedFilesFolder + "' -p=GlobalFileManifest='" +
                                         allFilesManifestPath + "' -p=ChangeFileMainfest='" + templateChangedManifestPath + 
                                         "' '"+ templateFilePath +"'";
                
                
                //ok now we can run the command line for the template
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"{templateCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                activeProcesses.Add(Process.Start(processStartInfo));

            }


            //loop through all the active processes and wait for them to finish
            bool doneGeneratingFiles = false;

            while (!doneGeneratingFiles)
            {
                doneGeneratingFiles = true;
                
                foreach (Process process in activeProcesses)
                {
                    //check if process is done 
                    if (process.HasExited)
                    {
                        
                        
                        // Read the output and error streams
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        
                        //print out the outpur
                        Console.WriteLine($"Process ended with stdOut: {output} and errors: {error} for command line arguments: {process.StartInfo.Arguments}");

                        break;
                    }
                    else
                    {
                        doneGeneratingFiles = false;
                    }
                }
            }

            //at this point the text gen should have finished and now we need to gather the generated files
            //check if they have a corresponding source file in the existing source file list

            List<string> allFilesInFolder = Directory.GetFiles(tempGeneratedFilesFolder).ToList();

            foreach (string newlyGeneratedFile in allFilesInFolder)
            {
                //remove the part of the path for the temp folder and replace it with the 
                //actual folder path
                string destinationFilePath =
                    newlyGeneratedFile.Replace(tempGeneratedFilesFolder, GeneratedFileOutputPath);

                //check if it exists in the invalid file list
                if (invalidGeneratedFiles.Contains(destinationFilePath))
                {
                    //remove the file from the invalid list
                    invalidGeneratedFiles.Remove(destinationFilePath);

                    //check if the new file is the same as the old file. we do this to skip copying to prevent 
                    //un necessary rebuilds of code
                    if (File.ReadAllText(destinationFilePath).Equals(File.ReadAllText(newlyGeneratedFile)) != true)
                    {
                        //copy the new file over the old file
                        File.WriteAllText(destinationFilePath, File.ReadAllText(newlyGeneratedFile));
                    }
                }
                else
                {
                    //just copy the file over as there is no existing file to compare to
                    File.WriteAllText(destinationFilePath, File.ReadAllText(newlyGeneratedFile));
                }

            }
            
            //print out list of all dirty files
            Console.WriteLine("all dirty templates");

            foreach (string templatefile in dirtyTemplateFiles)
            {
                Console.WriteLine($"template :{templatefile}");
            }
            
            Console.WriteLine("all dirty headers");

            foreach (string headerFile in dirtyHeaderFiles)
            {
                Console.WriteLine($"header :{headerFile}");
            }

            Console.WriteLine("all dirty source");
            foreach (string sourceFile in dirtySourceFiles)
            {
                Console.WriteLine($"Source :{sourceFile}");
            }
            
            Console.WriteLine("all dirty generated");
            foreach (string generatedFile in dirtyGeneratedFiles)
            {
                Console.WriteLine($"Source :{generatedFile}");
            }
            
            //loop through the remaining invalid files and delete them
            //this is because the sources they were built from changed 
            //and no template generated a replacement for them
            foreach (string invalidFile in invalidGeneratedFiles)
            {
                File.Delete(invalidFile);
            }

            return true;

        }
    }
}
