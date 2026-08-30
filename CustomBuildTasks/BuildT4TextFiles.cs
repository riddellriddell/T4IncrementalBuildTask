using System;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

//Custom build task debugging is done by making the "start" action for this project to open up ms build and run the debug project

namespace T4BuildTools
{
    //thin MSBuild adapter over the standalone TemplateCompiler pipeline
    public class BuildT4TextFiles : Task
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public ITaskItem[] InputFiles { get; set; } //all files that might be scanned and used for code gen

        [Required]
        public ITaskItem[] T4Templates { get; set; } //the text template to generate from

        [Required]
        public ITaskItem[] GeneratedFiles { get; set; } //the files that have already been created by the template file generator

        [Required]
        public string BaseIntermediateOutputPath { get; set; } //the temp folder to build into

        [Required]
        public string DefaultFileOutputPath { get; set; } //the desitnation folder for built files

        public override bool Execute()
        {
            //pack the MSBuild items/parameters onto the plain compiler API
            TemplateCompilerResult result = TemplateCompiler.Compile(
                Name,
                InputFiles.Select(item => item.ToString()).ToList(),
                T4Templates.Select(item => item.ToString()).ToList(),
                GeneratedFiles.Select(item => item.ToString()).ToList(),
                BaseIntermediateOutputPath,
                DefaultFileOutputPath,
                line => Log.LogMessage(MessageImportance.High, line));

            //forward any template failures to the MSBuild log
            foreach (string failure in result.TemplateFailures)
            {
                Log.LogError(failure);
            }

            return result.Success;
        }
    }
}