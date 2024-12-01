using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;

namespace T4BuildTools
{
    //this tool is to help scan the contents of files to extract information using regex
    public class FileScanUtility
    {
        //scan a file with a regex sting and return the result
        public static bool ScanFileWithRegex(string pattern, string filePath, out MatchCollection results)
        {
            try
            {
                //check if the file exists
                if (File.Exists(filePath) == false)
                {
                    Console.WriteLine("File not found");
                    results = null;
                    return false;
                }
            
                // Read the file content
                string fileContent = File.ReadAllText(filePath);
            
                //create the regex scanner
                Regex regex = new Regex(pattern);
            
                //apply it to the file contents
                MatchCollection matches = regex.Matches(fileContent);
            
                // Print matches
                Console.WriteLine($"Found {matches.Count} matches:");
                foreach (Match match in matches)
                {
                    Console.WriteLine(match.Value);
                }
                results = matches;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                results = null;
                return false;
            }

            return true;
        }

        public static List<string> ConvertMatchListToStringList(MatchCollection matches)
        {
            List<string> results = new List<string>();
            
            foreach (Match match in matches)
            {
                //check the number of groups
                GroupCollection groups = match.Groups;

                if (groups.Count > 1)
                {
                    results.Add(groups[1].Value);
                }
            }
            
            return results;
        }

        public static List<string> ConvertFileListToExistingFileList(List<string> fileList)
        {
            List<string> results = new List<string>();

            foreach (string filePath in fileList)
            {
                //check if the file exists
                if (File.Exists(filePath))
                {
                    results.Add(filePath);
                }
            }

            return results;
        }
        
        public static bool ConvertFileListToChangedSinceFileList(
            DateTime timeToCompareTo, 
            List<string> fileList,
            out List<string> changedFiles, 
            out List<string> deletedFiles)
        {
            bool didFileChange = false;
            
            changedFiles = new List<string>();
            deletedFiles = new List<string>();
            
            try
            {
  
                foreach (string filePath in fileList)
                {
                    //check if file exists
                    if (!File.Exists(filePath))
                    {
                        //add to deleted files
                        deletedFiles.Add(filePath);
                        
                        Console.WriteLine($"File {filePath} not found");
                        
                        didFileChange = true;
                        
                    }
                    else //check if file has changed
                    {
                        //get the last write time for the file
                        DateTime fileChangeTime = File.GetLastWriteTime(filePath);
                        
                        //check if it has changed since the target time
                        if (fileChangeTime >= timeToCompareTo)
                        {
                            changedFiles.Add(filePath);
                            
                            Console.WriteLine($"File {filePath} changed");
                            
                            didFileChange = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                changedFiles = new List<string>();
                deletedFiles = new List<string>();
                
                Console.WriteLine(e);
                return true;
            }

            return didFileChange;
        }
    }
}