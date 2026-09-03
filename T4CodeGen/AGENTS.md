# T4CodeGen

## Purpose

Console CLI front-end (`T4CodeGen.exe`) over the standalone `TemplateCompiler` API in `CustomBuildTasks.dll` — a second front-end beside the MSBuild task `BuildT4TextFiles` that runs the same incremental T4 pipeline from the command line, with matching output and exit-code/failure semantics.

## Ownership

- Owned by this folder.
- Consumed by scripts/CI/non-MSBuild pipelines and by developers debugging the pipeline standalone. It is an *additional* front-end — it does not replace the MSBuild task or `RunCodeGen.targets`.

## Local Contracts

- `T4CodeGen.csproj` — classic console app, .NET Framework v4.7.2, AnyCPU. Project-references `CustomBuildTasks.csproj` (brings in `TemplateCompiler`/`TemplateCompilerResult`) plus HintPath references to the vendored `..\tools\<package>\<version>\` assemblies so the exe bundles the same engine/Roslyn runtime set it needs at run time. Do not convert to `PackageReference` (standalone build, no restore/network).
- `Program.cs` — thin argument-to-API mapper only; it must not re-implement any pipeline logic (that lives in `TemplateCompiler.cs`). Parses the six task inputs (`Name`, `InputFiles`, `T4Templates`, `GeneratedFiles`, `BaseIntermediateOutputPath`, `DefaultFileOutputPath`) from CLI args or `@response.rsp`, calls `TemplateCompiler.Compile`, writes compiler log lines to stdout, forwards `TemplateFailures` to stderr verbatim (matching the task's `Log.LogError` text), and returns exit `0` on success / non-zero otherwise.
  - Lists are pipe (`|`) or semicolon (`;`) separated; response files have one argument per line (`#` = comment).
  - Run from the project directory with the same relative inputs the MSBuild task receives, so generated-file markers (`T4Gen_TemplateFile`/`T4Gen_InputFile`/`T4Gen_Destination`) come out identical.
  - Exit codes: `0` success; `1` any template failed; `2` bad command line / missing required arg.
- `README.md` — CLI usage and a worked example against the test bed.

## Work Guidance

## Verification

- `msbuild T4CodeGen\T4CodeGen.csproj` (offline) — must produce `T4CodeGen\bin\Debug\T4CodeGen.exe` with the engine/Roslyn runtime DLLs copied beside it from `tools\` only.
- Full regeneration must be byte-identical to a task-produced baseline: touch the test bed seeds/templates, run `T4CodeGen.exe` from the test bed project dir with the task's six inputs, and hash-compare the regenerated `*.t4generated.*` against a task-produced set.
- Exit code `0` on a clean incremental no-op; non-zero with the template named on stderr when a template is deliberately broken.

## Child DOX Index

None.
