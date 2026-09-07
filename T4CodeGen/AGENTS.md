# T4CodeGen

## Purpose

Console CLI front-end (`T4CodeGen.exe`) over the standalone `TemplateCompiler` API, which this project owns directly — a second front-end beside the MSBuild task `BuildT4TextFiles` that runs the same incremental T4 pipeline from the command line, with matching output and exit-code/failure semantics. Self-contained: one `.csproj`, one folder, no `ProjectReference` to `CustomBuildTasks`. This folder is the exe's copy/deployment boundary — it owns the single vendored `tools\` engine/compiler/runtime assembly tree that both the CLI and the MSBuild task consume via HintPath.

## Ownership

- Owned by this folder.
- Consumed by scripts/CI/non-MSBuild pipelines and by developers debugging the pipeline standalone. It is an *additional* front-end — it does not replace the MSBuild task or `RunCodeGen.targets`.

## Local Contracts

- `T4CodeGen.csproj` — classic console app, .NET Framework v4.7.2, AnyCPU. Self-contained: has **no** `ProjectReference` to `CustomBuildTasks`. Compiles `TemplateCompiler.cs` and `FileScanUtility.cs` directly (single source of truth — `CustomBuildTasks` pulls the same files in via linked-source), plus HintPath references to the vendored `tools\<package>\<version>\` assemblies so the exe bundles the same engine/Roslyn runtime set it needs at run time. Do not convert to `PackageReference` (standalone build, no restore/network).
- `TemplateCompiler.cs` — the standalone, MSBuild-independent generation pipeline (namespace `T4BuildTools`), single source of truth owned here. Also compiled into `CustomBuildTasks.dll` via linked-source `<Compile Include Link>` (see `CustomBuildTasks/AGENTS.md`). Contracts documented under `CustomBuildTasks/AGENTS.md`.
- `FileScanUtility.cs` — static helpers (`ScanFileWithRegex`, `ConvertMatchListToStringList`, `ConvertFileListToExistingFileList`, `ConvertFileListToChangedSinceFileList`), single source of truth owned here; linked-compiled into `CustomBuildTasks` the same way.
- `Program.cs` — thin argument-to-API mapper only; it must not re-implement any pipeline logic (that lives in `TemplateCompiler.cs`). Parses the six task inputs (`Name`, `InputFiles`, `T4Templates`, `GeneratedFiles`, `BaseIntermediateOutputPath`, `DefaultFileOutputPath`) from CLI args or `@response.rsp`, calls `TemplateCompiler.Compile`, writes compiler log lines to stdout, forwards `TemplateFailures` to stderr verbatim (matching the task's `Log.LogError` text), and returns exit `0` on success / non-zero otherwise.
  - Lists are pipe (`|`) or semicolon (`;`) separated; response files have one argument per line (`#` = comment).
  - Run from the project directory with the same relative inputs the MSBuild task receives, so generated-file markers (`T4Gen_TemplateFile`/`T4Gen_InputFile`/`T4Gen_Destination`) come out identical.
  - Exit codes: `0` success; `1` any template failed; `2` bad command line / missing required arg.
- `README.md` — CLI usage and a worked example against the test bed.
- `tools/` — the vendored third-party assembly tree (one folder per `package\version`, with license files): `Mono.TextTemplating` 3.0.0 + `Mono.TextTemplating.Roslyn` 3.0.0, Roslyn `Microsoft.CodeAnalysis*`, and the `System.*` runtime deps. The single copy of the engine/compiler/runtime assemblies; `T4CodeGen.csproj` (project-local `tools\`) and `CustomBuildTasks.csproj` (`..\T4CodeGen\tools\`) both reference it via HintPath. Keep committed — the standalone build's only dependency copies. `.gitignore` whitelists `T4CodeGen/tools/**` (negation) so the machine-global `*.dll` ignore rule cannot exclude the vendored binaries.

## Work Guidance

## Verification

- `msbuild T4CodeGen\T4CodeGen.csproj` (offline) — must produce `T4CodeGen\bin\Debug\T4CodeGen.exe` with the engine/Roslyn runtime DLLs copied beside it from `T4CodeGen\tools\` only, and **no** `CustomBuildTasks.dll` in `bin\Debug`.
- Automated: root `test.bat` (or build `T4CodeGenTests` + run `bin\Debug\T4CodeGenTests.exe`) — black-box case battery over the exe: exit codes `0`/`1`/`2`, response files, `|`/`;` list separators, byte-identical no-op reruns, dirty-input regeneration, broken-template isolation, `-h` help. Must print `PASS` per case and exit `0`. See `T4CodeGenTests/AGENTS.md`.
- Full regeneration must be byte-identical to a task-produced baseline: touch the test bed seeds/templates, run `T4CodeGen.exe` from the test bed project dir with the task's six inputs, and hash-compare the regenerated `*.t4generated.*` against a task-produced set.
- Exit code `0` on a clean incremental no-op; non-zero with the template named on stderr when a template is deliberately broken.

## Child DOX Index

None.
