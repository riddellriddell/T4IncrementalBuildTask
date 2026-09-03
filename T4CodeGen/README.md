# T4CodeGen — CLI front-end for the standalone T4 compiler

`T4CodeGen.exe` is a command-line front-end over the same standalone `TemplateCompiler.Compile` API the MSBuild task (`BuildT4TextFiles`) hosts. It runs the identical incremental T4 pipeline (dirty scan, `.T4ChangedManifest` writes, in-process engine + Roslyn, destination copy/skip, invalid-file deletion, per-template failure isolation) and mirrors the task's success/failure semantics: **exit `0` on success, non-zero if any template failed.**

It is an *additional* front-end — it does not replace the MSBuild task or `RunCodeGen.targets`.

## Build

```
msbuild T4CodeGen\T4CodeGen.csproj
```

Output: `T4CodeGen\bin\Debug\T4CodeGen.exe` (plus the engine/Roslyn runtime assemblies copied beside it). Builds standalone from the vendored `tools\` assemblies only — no NuGet restore, no network, no `t4.exe`, no PATH entry.

## Usage

```
T4CodeGen -Name <name> -InputFiles <list> -T4Templates <list> -GeneratedFiles <list>
          -BaseIntermediateOutputPath <path> -DefaultFileOutputPath <path> [@response.rsp]
```

### Arguments

| Flag | Meaning |
|------|---------|
| `-Name <name>` | build name (e.g. `T4IncrementalBuild`). |
| `-InputFiles <list>` | seed source/header files scanned for changes. |
| `-T4Templates <list>` | the `.tt` templates to run. |
| `-GeneratedFiles <list>` | already-generated `*.t4generated.*` outputs (used for invalidation). |
| `-BaseIntermediateOutputPath <path>` | folder for build state + temp `GeneratedFiles` (the `GlobalFileManifest.T4Manifest` lives here). |
| `-DefaultFileOutputPath <path>` | default folder generated files are copied back to. |
| `@response.rsp` | read additional args from a response file (one argument per line; `#` starts a comment line). |
| `-h` / `-help` / `-?` | show help and exit. |

**Lists** are pipe (`|`) or semicolon (`;`) separated path values.

### Working directory

Run the exe from the **project directory** and pass the same **relative** inputs the MSBuild task receives, so the generated-file manifest/destination markers (`T4Gen_TemplateFile(...)`, `T4Gen_InputFile(...)`, `T4Gen_Destination(...)`) come out identical.

### Exit codes

- `0` — successful run (all templates ran or were skipped cleanly).
- `1` — at least one template failed; the failure is written to stderr naming the template, matching the task's `Log.LogError` text.
- `2` — invalid command line / missing required argument.

## Example against the test bed

From `D:\Game Dev\T4CodeGenLibrary\T4IntegrationTestBed`:

```
T4CodeGen.exe -Name T4IncrementalBuild ^
  -InputFiles "FancyWrite.h|FancyWrite.cpp|Main.cpp" ^
  -T4Templates "T4Templates\HeaderExample.tt|T4Templates\TestTemplate.tt" ^
  -GeneratedFiles "FancyWrite_HeaderExample.t4generated.h|FancyWrite_TestHeader.t4generated.h|Main_TestHeader.t4generated.h|TestTemplate.t4generated.txt" ^
  -BaseIntermediateOutputPath "D:\Game Dev\T4CodeGenLibrary\T4IntegrationTestBed\x64\Debug\obj" ^
  -DefaultFileOutputPath "D:\Game Dev\T4CodeGenLibrary\T4IntegrationTestBed"
```

Or with a response file (`test.rsp`, one argument per line):

```
@test.rsp
```

## Notes

- Do not re-implement pipeline logic in the exe — the CLI is a thin argument-to-API mapper plus stdout/stderr rendering. Any pipeline fix belongs in `TemplateCompiler.cs`.
- The compiler's per-template failures are forwarded from `TemplateCompilerResult.TemplateFailures` verbatim, so CLI error text equals the MSBuild task's error text.
