# T4CodeGen.exe — CLI front-end for the standalone T4 compiler (integration contract)

`T4CodeGen.exe` is a command-line front-end over the same standalone `TemplateCompiler.Compile` API the MSBuild task (`BuildT4TextFiles`) hosts. It runs the identical incremental T4 pipeline (dirty scan, `.T4ChangedManifest` writes, in-process engine + Roslyn, destination copy/skip, invalid-file deletion, per-template failure isolation) and mirrors the task's success/failure semantics.

This document is the **normative integration contract** for the exe: it specifies the observable behavior exactly — response-file grammar, argument semantics, exit codes, stdout/stderr channels, and the `T4Gen_*` output markers and template parameters a consumer authors against. `T4CodeGen/Program.cs` is a thin argument-to-API mapper and `T4CodeGen/TemplateCompiler.cs` is the pipeline; where prose here disagrees with the code, the code wins.

## Integration contract

### Response files

An argument of the form `@<path>` (at least two characters, so a bare `@` is an ordinary argument) names a response file.

- Each line of the file is **trimmed**; blank lines and lines whose first non-blank character is `#` are skipped; every remaining line becomes exactly **one** argument. Quotes are not stripped and no line is split on spaces, so a flag and its value each go on their own line and paths containing spaces stay intact without quoting.
- Response-file arguments are expanded **in place, left to right**, and can be mixed with ordinary command-line arguments. Non-`@` arguments pass through unchanged.
- A missing `@<path>` writes `Response file not found: <path>` to **stderr** and exits `2` immediately — no usage is printed and this error wins over everything, including help tokens elsewhere on the command line.

Example `response.rsp`:

```
# full build against the test bed
-Name
T4IncrementalBuild
-InputFiles
FancyWrite.h|FancyWrite.cpp|Main.cpp
-T4Templates
T4Templates\HeaderExample.tt|T4Templates\TestTemplate.tt
-BaseIntermediateOutputPath
D:\Game Dev\T4CodeGenLibrary\T4IntegrationTestBed\x64\Debug\obj
-DefaultFileOutputPath
D:\Game Dev\T4CodeGenLibrary\T4IntegrationTestBed
```

Invoke it with `T4CodeGen @response.rsp` run from the working directory the paths are relative to.

### Arguments

The exe takes six data inputs plus the `@file` token and the help aliases:

| Argument | Kind | Required | Semantics |
|---|---|---|---|
| `-Name` | scalar | yes | build name (e.g. `T4IncrementalBuild`). |
| `-InputFiles` | list | yes, ≥ 1 | seed source/header files scanned for changes. |
| `-T4Templates` | list | yes, ≥ 1 | the `.tt` templates to run. |
| `-GeneratedFiles` | list | **no** | already-generated `*.t4generated.*` outputs; feeds change-based invalidation only. |
| `-BaseIntermediateOutputPath` | scalar | yes | folder for build state + temp `GeneratedFiles`. See path formulas below. |
| `-DefaultFileOutputPath` | scalar | yes | default folder generated files are copied back to. |
| `@file` | token | no | expands to its file's arguments; see "Response files". |
| `-h` / `-help` / `-?` / `/?` | help | — | prints usage to **stdout** and exits `0`. |

Grammar rules:

- **Flags are case-insensitive.** `-NAME`, `-InputFiles`, and `-inputfiles` are accepted identically; values are used verbatim.
- **Help wins over everything.** If any argument equals (exact, case-sensitive match) `-h`, `-help`, `-?`, or `/?` — including one that appears *inside* an expanded response file or alongside real arguments — usage is printed to stdout and the exe exits `0` before any parsing.
- **Scalars last-write-wins; lists accumulate.** Repeating `-InputFiles`/`-T4Templates`/`-GeneratedFiles` appends to the list; repeating a scalar flag overwrites the previous value (last occurrence wins).
- **Lists** split on `|` when the value contains a `|`, otherwise on `;` — so a value mixing both separators splits on `|` only. Empty entries are dropped, each entry is trimmed, and entries are de-duplicated (case-sensitive exact equality).
- **One argument per flag.** Each flag consumes the single next argument as its value. A flag that is the last argument reports `Missing value for argument: <flag>` and exits `2`.
- **Anything else** reports `Unknown argument: <flag>` and exits `2`. For both the unknown-flag and missing-value errors, usage is printed to stderr.
- **Required-argument check** runs after parsing, in the order `-Name`, `-InputFiles`, `-T4Templates`, `-BaseIntermediateOutputPath`, `-DefaultFileOutputPath`; the first missing one reports `Missing required argument: <flag>` and exits `2` (with usage on stderr). `-GeneratedFiles` is never required.
- **Path formulas.** The two build-state artifacts are formed by **literal string concatenation** onto the raw `-BaseIntermediateOutputPath` value, with no separator inserted:
  - Global build state: `<BaseIntermediateOutputPath>` + `GlobalFileManifest.T4Manifest`
  - Temp generation folder: `<BaseIntermediateOutputPath>` + `GeneratedFiles`

  Pass a value ending in a path separator — e.g. `...\x64\Debug\obj` produces `...\x64\Debug\objGeneratedFiles` and `...\x64\Debug\objGlobalFileManifest.T4Manifest` — or accept the concatenated names.

**Working directory.** Run the exe from the **project directory** and pass the same **relative** inputs the MSBuild task receives, so the generated-file destination and marker paths come out identical.

### Exit codes

| Code | Meaning | Channel output |
|---|---|---|
| `0` | Success: every template compiled, or was skipped cleanly; no template failed. | Nothing on stderr. |
| `1` | At least one template failed, after all templates were attempted (healthy ones still produced their outputs). | One `T4 Template <path> failed: …` line per failing template on stderr. |
| `2` | Invalid invocation. See the five reachable paths below. | Error text (+ usage where noted) on stderr, except the zero-args path. |

The five `2` paths, each with its exact stdout/stderr composition:

1. **Zero arguments** → usage on **stdout**, nothing on stderr, exit `2`.
2. **Missing response file** (`@missing.rsp`) → `Response file not found: missing.rsp` on **stderr**, no usage, exit `2`.
3. **Unknown flag** → `Unknown argument: <flag>` + usage on **stderr**, exit `2`.
4. **Missing value** (a flag as the last argument) → `Missing value for argument: <flag>` + usage on **stderr**, exit `2`.
5. **Missing required argument** → `Missing required argument: <flag>` + usage on **stderr**, exit `2`.

### stdout vs stderr

- **stdout** is the compiler-log channel: every log line the `TemplateCompiler.Compile` pipeline emits, plus any direct output from the in-process templates (template `Console.WriteLine`s, `ScanFileWithRegex` match dumps, and so on). Usage goes to stdout in the two situations that are not an error: "help requested" (exit `0`) and "zero arguments" (exit `2`).
- **stderr** is the error/failure channel only: parse errors, usage attached to a parse error, `Response file not found: …`, and the forwarded per-template failure lines (exit `1`).
- A successful run (exit `0`) writes **nothing** to stderr.

### Generated-file markers and template parameters

The compiler tracks dependencies by scanning generated files with three regular expressions; group 1 of every match is the path:

| Marker | Scan regex | Meaning |
|---|---|---|
| `T4Gen_TemplateFile(<path>)` | `T4Gen_TemplateFile\((.*?)\)` | the template that generated this file. The file is invalidated if its template changed or was deleted since the last build. |
| `T4Gen_InputFile(<path>)` | `T4Gen_InputFile\((.*?)\)` | a seed input the file depends on (one per input). The file is invalidated if any input changed or was deleted since the last build. |
| `T4Gen_Destination(<folder>)` | `T4Gen_Destination\((.*?)\)` | optional; the folder the file is copied back to (see below). |

Every generated file should embed the `T4Gen_TemplateFile` and `T4Gen_InputFile` markers. The templates emit them as `//T4Gen_TemplateFile(<path>)` / `//T4Gen_InputFile(<path>)` comment lines, and consumers grepping generated files should match that literal `//` prefix; the scan is regex-based, so any comment notation works. `T4Gen_RUN_TEXT_TEMPLATE_ON_THIS(<name>)` is a separate tag convention *inside seed files* (e.g. `FancyWrite.h`) that `HeaderExample.tt` scans for — it is a template-authoring convention, not an exe-channel contract.

**Copy destination.** Each generated file lands in the temp folder `<BaseIntermediateOutputPath>` + `GeneratedFiles`, and the compiler copies it back by default to `DefaultFileOutputPath` with that temp prefix replaced. An embedded `T4Gen_Destination(<folder>)` marker designates a different copy-back folder. **Known caveat:** these destination strings are computed in `TemplateCompiler.cs` (`newlyGeneratedFile.Replace(tempGeneratedFilesFolder, defaultFileOutputPath)` for the default, the `T4Gen_Destination` branch around `TemplateCompiler.cs:421-434` for the override), but the override branch is guarded by the inverted condition `if (!didSucceed)`, so on the normal scan-success path the override is not applied. The destination-marker contract is the documented intent; the inversion is a candidate `BUG_` investigation, out of scope for this contract.

**Template parameters.** Templates must declare three string parameters, which the compiler feeds the in-process engine via `AddParameter` (resolved through `<#@ parameter type="System.String" name="..." #>`):

| Parameter | Value |
|---|---|
| `OutputFolder` | `<BaseIntermediateOutputPath>` + `GeneratedFiles` — the temp folder; write outputs here with a `.t4generated.<ext>` extension. |
| `ChangeFileManifest` | `<TemplateName>.T4ChangedManifest`, written next to the template with one dirty file path per line — read this to know which inputs this run regenerates. |
| `GlobalFileManifest` | `<BaseIntermediateOutputPath>` + `GlobalFileManifest.T4Manifest` — the global last-build-state file. |

**Per-template failures** are reported as `T4 Template <template path> failed: <error text>` — one line per failing template, forwarded to stderr verbatim → exit `1`. The tail is engine-generated error text and is **not** byte-stable; only the `T4 Template … failed:` prefix is contractual.

### Test coverage mapping

The black-box harness (`T4CodeGenTests/`, run by `test.bat`) asserts this contract from the process boundary; the full case list and harness mechanics live in `T4CodeGenTests/AGENTS.md`.

Contract statement → pinned by:

- Exit `0` success; byte-identical regeneration; byte-identical no-op rerun; input-change invalidation: `FreshRegeneration`, `NoOpRerun`, `DirtyInputRegenerates`.
- Exit `1` + per-template failure on stderr + healthy outputs still produced: `BrokenTemplateIsolation`.
- Exit `2` — unknown flag, missing required argument, missing value, zero args, missing response file: `UnknownFlagExits2`, `MissingNameExits2`, `MissingValueExits2`, `ZeroArgsExits2`, `ResponseFileMissingExits2`.
- Help aliases (`-h`, `-help`, `-?`, `/?`) and help-anywhere: `HelpExitZero`, `HelpAliasesExitZero`.
- `|` ≡ `;` list separators: `ListSeparators`.
- Response file ≡ explicit args, including comment lines, blank lines, and trimming: `ResponseFile`, `ResponseFileCommentsAndTrimming`.
- Case-insensitive flags: `CaseInsensitiveFlags`.
- Repeated-flag accumulation and de-dup: `RepeatedFlagAppends`.
- Successful runs write nothing to stderr: asserted inside the new equivalence cases (`CaseInsensitiveFlags`, `RepeatedFlagAppends`, `ResponseFileCommentsAndTrimming`).

Documented but not yet pinned by the harness:

- `-GeneratedFiles` optional on a *successful* run (existing cases either supply it or fail before the pipeline runs).
- The exact marker-regex text and the three parameter path formulas (exercised behaviorally by the invalidation cases, not asserted textually).
- `T4Gen_Destination` override behavior (unobservable given the inverted code branch above).
- The engine-generated failure-text tails are explicitly non-contractual.

## Build

```
msbuild T4CodeGen\T4CodeGen.csproj
```

Output: `T4CodeGen\bin\Debug\T4CodeGen.exe` (plus the engine/Roslyn runtime assemblies copied beside it). Builds standalone from the vendored `T4CodeGen\tools\` assemblies only — no NuGet restore, no network, no `t4.exe`, no PATH entry.

## Usage

```
T4CodeGen -Name <name> -InputFiles <list> -T4Templates <list> -GeneratedFiles <list>
          -BaseIntermediateOutputPath <path> -DefaultFileOutputPath <path> [@response.rsp]
```

### Example against the test bed

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