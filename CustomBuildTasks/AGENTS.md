# CustomBuildTasks

## Purpose

C# MSBuild build task library (assembly `CustomBuildTasks`, namespace `T4BuildTools`) that runs T4 text templates against C++ sources incrementally during a Visual Studio build.

## Ownership

- Owned by this folder.
- Consumed by the test bed: the root `RunCodeGen.targets` loads `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` and calls `T4BuildTools.BuildT4TextFiles`.

## Local Contracts

- `CustomBuildTasks.csproj` — classic C# class library, .NET Framework v4.7.2, AnyCPU. Output `bin\Debug\CustomBuildTasks.dll` is the exact path hardcoded by the root `RunCodeGen.targets`; do not move the output or switch to SDK-style layout without updating that reference. Engine/Roslyn assemblies are referenced via `<Reference><HintPath>` into the vendored `..\tools\<package>\<version>\` — do not convert back to `PackageReference`/`packages.config` (standalone build, no restore/network).
- `BuildT4TextFiles.cs` — the main task. Required parameters: `Name`, `InputFiles` (seed sources), `T4Templates`, `GeneratedFiles`, `BaseIntermediateOutputPath`, `DefaultFileOutputPath`.
  - Incremental state: last build time is read from `GlobalFileManifest.T4Manifest` in `BaseIntermediateOutputPath`; a missing manifest forces full regeneration.
  - Dirty set: `InputFiles` or `T4Templates` whose last-write time is newer than the last build. A dirty template marks every `InputFiles` entry dirty for that template; dirty inputs are added to all templates.
  - Dependency tracking is comment-based: generated files must embed `T4Gen_TemplateFile(<path>)` and `T4Gen_InputFile(<path>)` comments. A generated file whose template or any input changed or was deleted since its timestamp is invalid — regenerated, or deleted if no template replaces it.
  - Writes a per-template `<TemplateName>.T4ChangedManifest` next to each `.tt`, then runs that template **in-process** via a `Mono.TextTemplating.TemplateGenerator` + `Mono.TextTemplating.Roslyn` `UseInProcessCompiler()` (see `ProcessTemplateInProcess`). Passes the three template parameters via `AddParameter(null, null, <name>, <value>)`: `OutputFolder`, `ChangeFileManifest`, `GlobalFileManifest`. No `t4.exe`, no `powershell.exe`, no `PATH` dependency.
  - Copies each new generated file from `BaseIntermediateOutputPath\GeneratedFiles\` to `DefaultFileOutputPath`, or to the folder named by an optional `T4Gen_Destination(<folder>)` comment; skips the write when content is byte-identical to avoid needless rebuilds.
- `FileScanUtility.cs` — static helpers: `ScanFileWithRegex`, `ConvertMatchListToStringList` (group 1 only), `ConvertFileListToExistingFileList`, `ConvertFileListToChangedSinceFileList`.
- `AddMatchingFilesToOutput.cs` — prototype logging task (`TargetSeedFiles`, `TargetT4File`); compiled but not referenced by any target. Deliberately still uses the old `t4 -v`/`ProcessStartInfo` approach — exclude it from any "no `t4`/`Process` shell-out" grep sweep; the in-process engine requirement applies to `BuildT4TextFiles.cs` only.
- `Debug.testproj` — legacy standalone MSBuild harness for task debugging. Stale: uses old parameter names (`TargetT4Files`, `HeaderFiles`, `SourceFiles`, …) and a hardcoded absolute `AssemblyFile` path; treat as reference only.

## Work Guidance

## Verification

- `msbuild CustomBuildTasks.csproj` — must produce `bin\Debug\CustomBuildTasks.dll` without errors (the root targets depend on that path). The engine step is self-contained: `bin\Debug` carries `Mono.TextTemplating.dll`, `Mono.TextTemplating.Roslyn.dll`, and the `Microsoft.CodeAnalysis*`/`System.*` runtime assemblies from `tools\`.
- Full pipeline check: build the library, then the test bed (see `agents/buildguild.md`) and confirm `GenerateT4Files` regenerates the test bed's `*.t4generated.*` files in-process (no `t4` on PATH required).

## Child DOX Index

None.
