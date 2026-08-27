# CustomBuildTasks

## Purpose

C# MSBuild build task library (assembly `CustomBuildTasks`, namespace `T4BuildTools`) that runs T4 text templates against C++ sources incrementally during a Visual Studio build.

## Ownership

- Owned by this folder.
- Consumed by the test bed: the root `RunCodeGen.targets` loads `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` and calls `T4BuildTools.BuildT4TextFiles`.

## Local Contracts

- `CustomBuildTasks.csproj` — classic C# class library, .NET Framework v4.7.2, AnyCPU. Output `bin\Debug\CustomBuildTasks.dll` is the exact path hardcoded by the root `RunCodeGen.targets`; do not move the output or switch to SDK-style layout without updating that reference.
- `BuildT4TextFiles.cs` — the main task. Required parameters: `Name`, `InputFiles` (seed sources), `T4Templates`, `GeneratedFiles`, `BaseIntermediateOutputPath`, `DefaultFileOutputPath`.
  - Incremental state: last build time is read from `GlobalFileManifest.T4Manifest` in `BaseIntermediateOutputPath`; a missing manifest forces full regeneration.
  - Dirty set: `InputFiles` or `T4Templates` whose last-write time is newer than the last build. A dirty template marks every `InputFiles` entry dirty for that template; dirty inputs are added to all templates.
  - Dependency tracking is comment-based: generated files must embed `T4Gen_TemplateFile(<path>)` and `T4Gen_InputFile(<path>)` comments. A generated file whose template or any input changed or was deleted since its timestamp is invalid — regenerated, or deleted if no template replaces it.
  - Writes a per-template `<TemplateName>.T4ChangedManifest` next to each `.tt`, then runs `t4 -p=OutputFolder='<temp>' -p=GlobalFileManifest='<path>' -p=ChangeFileMainfest='<path>' '<template>.tt'` via `powershell.exe` (requires `t4` on PATH).
  - Copies each new generated file from `BaseIntermediateOutputPath\GeneratedFiles\` to `DefaultFileOutputPath`, or to the folder named by an optional `T4Gen_Destination(<folder>)` comment; skips the write when content is byte-identical to avoid needless rebuilds.
- `FileScanUtility.cs` — static helpers: `ScanFileWithRegex`, `ConvertMatchListToStringList` (group 1 only), `ConvertFileListToExistingFileList`, `ConvertFileListToChangedSinceFileList`.
- `AddMatchingFilesToOutput.cs` — prototype logging task (`TargetSeedFiles`, `TargetT4File`); compiled but not referenced by any target.
- `Debug.testproj` — legacy standalone MSBuild harness for task debugging. Stale: uses old parameter names (`TargetT4Files`, `HeaderFiles`, `SourceFiles`, …) and a hardcoded absolute `AssemblyFile` path; treat as reference only.

## Work Guidance

## Verification

- `msbuild CustomBuildTasks.csproj` — must produce `bin\Debug\CustomBuildTasks.dll` without errors (the root targets depend on that path).
- Full pipeline check: build `T4IntegrationTestBed.sln` and confirm `GenerateT4Files` regenerates the test bed's `*.t4generated.*` files (requires `t4` on PATH).

## Child DOX Index

None.
