using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace T4BuildTools
{
    public class AddMatchingFilesToOutput : Task
    {
        [Required]
        public string Name { get; set; }


        [Required]
        public ITaskItem[] TargetSeedFiles { get; set; } //all the files that may be requesting code generation


        [Required]
        public ITaskItem[] TargetT4File { get; set; } //the text template to generate from

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Hello{0}", Name);

            Log.LogMessage(MessageImportance.High, "List of all files that could need generation", Name);
            //log all the files to process
            for (int i = 0; i < TargetSeedFiles.Length; i++)
            {
                //file
                Log.LogMessage(MessageImportance.High, $"File to generate for {TargetSeedFiles[i].ToString()} with item spec {TargetSeedFiles[i].ItemSpec}");
            }

            Log.LogMessage(MessageImportance.High, "List of T4 files that could need generation", Name);
            //log all the files to process
            for (int i = 0; i < TargetT4File.Length; i++)
            {
                //file
                Log.LogMessage(MessageImportance.High, $"File to generate {TargetT4File[i].ToString()}");

                //file meta data
                foreach (var names in TargetT4File[i].MetadataNames)
                {
                    Log.LogMessage(MessageImportance.High, $"Meta name {names}");
                }
            }

            for (int i = 0; i < TargetT4File.Length; i++)
            {
                // Define the process start information
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe", // You can change this to the specific executable you're running
                    /*Arguments = "/c echo Hello from the command line!",*/
                    Arguments = $"/c t4 -v {TargetT4File[i].GetMetadata("FullPath")}", // Your command line arguments here*/
                    RedirectStandardOutput = true, // Capture the output
                    RedirectStandardError = true,   // Capture error output
                    UseShellExecute = false,        // Must be false to redirect output
                    CreateNoWindow = true           // Don't create a console window
                };


                //for each t4 text item run code gen

                // Create the process and start it
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    // Read the standard output and error output
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // Wait for the process to exit
                    process.WaitForExit();

                    // Output the results
                    Log.LogMessage("Output: " + output);
                    Log.LogMessage("Error: " + error);
                }

                //get the root directorty
                string rootDirectory = TargetT4File[i].GetMetadata("RootDir");

                //get the directory of the file
                string directory = TargetT4File[i].GetMetadata("Directory");

                //get the file name excluding the extension
                string fileName = TargetT4File[i].GetMetadata("Filename");

                //get the file extension
                string fileExtension = TargetT4File[i].GetMetadata("Extension");

                Log.LogMessage(MessageImportance.High, $"searching for files created with path: {directory}{fileName}.*");

                //find the files created and add them as output
                // Get all matching files in the folder
                string[] files = Directory.GetFiles(rootDirectory + directory, fileName + ".*");

                string sourceFilePath = rootDirectory + directory + fileName + fileExtension;

                Log.LogMessage(MessageImportance.High, $"{files.Length} found skipping source file {sourceFilePath}");

                // Loop through the results and print the file paths
                foreach (string file in files)
                {
                    Log.LogMessage(MessageImportance.High, $"Found File {file}");

                    //skip files with matching path
                    if(sourceFilePath == file)
                    {
                        Log.LogMessage(MessageImportance.High, $"Skipping source file");
                        continue;
                    }

                    //adding output to meta info for file
                }
            }

            return true;
        }
    }
}
