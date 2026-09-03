# Goal 2.2 Implementation Plan: `.exe` CLI Front-End on the Standalone Compiler API

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `GOAL`
- Task Name: Goal 2.2 — CLI `.exe` front-end
- Status: `Complete`
- Owner: "Your Name"
- Last Updated: `2026-09-03`

## Linked Context

- Design: [design.md](../design.md)
- Milestone: [milestones.md](../milestones.md)
- Goal: [goals2.md](../goals2.md)

## Objective

Ship a command-line `.exe` front-end (`T4CodeGen.exe`) that drives the same standalone `TemplateCompiler.Compile` API the MSBuild task uses, so the pipeline is reachable from scripts, CI steps, and non-MSBuild pipelines and is debuggable standalone — with exit-code/failure semantics mirroring the task.

## Problem Summary

Goal 2.1 extracted the whole generation pipeline into the MSBuild-independent `TemplateCompiler.Compile(name, inputFiles, t4Templates, generatedFiles, baseIntermediateOutputPath, defaultFileOutputPath, log)` API and reduced `BuildT4TextFiles` to a thin adapter. But the only front-end is still the MSBuild task via `RunCodeGen.targets`. There is no standalone entry point, so scripts/CI/non-MSBuild callers and standalone debugging have nowhere to hook in.

## Scope

- In scope: new console project `T4CodeGen` (classic .NET Framework 4.7.2) added to `T4IntegrationTestBed.sln`; `Program.cs` maps CLI args/response file onto `TemplateCompiler.Compile`, prints log lines to stdout, returns exit code 0 / non-zero; csproj references `CustomBuildTasks` and the vendored `tools\` assemblies by HintPath; usage doc; DOX status updates.
- Out of scope: no new T4 features, no retargeting off .NET Framework 4.7.2, no replacement of the MSBuild task / `RunCodeGen.targets` (the exe is an additional front-end), no change to the incremental-scan algorithm / comment-marker contract / destination resolution / failure semantics.

## Current State

`TemplateCompiler.cs` (`CustomBuildTasks.dll`, namespace `T4BuildTools`) exposes `TemplateCompiler.Compile(...) -> TemplateCompilerResult` (`Success` + `TemplateFailures`), all diagnostics through the `Action<string>` sink. `BuildT4TextFiles.cs` already demonstrates the exact call shape and failure forwarding. The vendored engine/Roslyn/runtime assemblies live under `tools\` and are referenced by HintPath; `CustomBuildTasks\bin\Debug\` carries a runtime copy. Templates and manifest markers use paths relative to the project directory (the working directory MSBuild uses when building the vcxproj), so the CLI must be run from that same working directory with the same relative inputs to reproduce identical output.

## Assumptions and Constraints

- The task's runtime contract is the template: `Compile` receives the same `Name`, `InputFiles`, `T4Templates`, `GeneratedFiles`, `BaseIntermediateOutputPath`, `DefaultFileOutputPath` the task gets from `RunCodeGen.targets`, with relative paths evaluated from the project directory.
- Refactor by reuse, not rewrite: the CLI must not re-implement any pipeline logic — it is a thin argument-to-API mapping plus stdout rendering, exactly paralleling the task adapter.
- Reuse the existing per-template failure text (already stored in `result.TemplateFailures`) so CLI error output matches the task's `Log.LogError` text.
- Exit `0` on success; non-zero (e.g. `1`) if `!result.Success`. A bad command line (missing required args) also exits non-zero with a usage message.
- The CLI builds standalone from the vendored `tools\` assemblies only — no NuGet restore, no network.
- The CLI project must not depend on `Microsoft.Build.*`; it references `CustomBuildTasks.dll` and the engine runtime assemblies.

## Files and Areas Likely Affected

- `T4CodeGen/T4CodeGen.csproj` — new console project (new directory under the solution root), .NET Framework 4.7.2, references `CustomBuildTasks.csproj` + HintPath into `..\tools\`.
- `T4CodeGen/Program.cs` — CLI entry point (argument parsing, response file, `TemplateCompiler.Compile` call, stdout log sink, exit-code mapping, usage text).
- `T4CodeGen/T4CodeGen.usage.md` (or `agents/`) — usage doc with a worked example against the test bed.
- `T4IntegrationTestBed.sln` — add the `T4CodeGen` project.
- Docs: `CustomBuildTasks/AGENTS.md`, `agents/plans/design.md`, `agents/plans/goals2.md`, `agents/plans/milestones.md`, `agents/plans/AGENTS.md`, `agents/buildguild.md`, root `AGENTS.md`.

## Implementation Steps

1. Create `T4CodeGen/` with `T4CodeGen.csproj` (OutputType `Exe`, `TargetFrameworkVersion v4.7.2`, project reference to `CustomBuildTasks.csproj` or direct DLL reference to `bin\Debug\CustomBuildTasks.dll`, HintPath references to the same `..\tools\<pkg>\<ver>` assemblies the library uses).
2. Implement `Program.cs`: parse CLI args for `Name`, `InputFiles`, `T4Templates`, `GeneratedFiles`, `BaseIntermediateOutputPath`, `DefaultFileOutputPath` (from explicit args or `@response.rsp`), pass each repeatable list via a delimiter or repeated flags; call `TemplateCompiler.Compile` with a log sink of `Console.WriteLine`; print `result.TemplateFailures` to stderr (matching task error text); return `0` or non-zero.
3. Add the project to `T4IntegrationTestBed.sln` with the `.NET Framework` project GUID type and config mappings across Debug/Release × Any CPU/x64/x86.
4. Build the library, then build `T4CodeGen`, then run `T4CodeGen.exe` from the test bed project directory against the seed files/templates and compare the regenerated `*.t4generated.*` to the MSBuild-produced baseline.
5. Verify exit-code semantics: `0` on a clean incremental run, non-zero when a template is deliberately broken.
6. Write the usage doc; update all DOX status docs.

## Verification Plan

### Automated Checks

- `msbuild T4CodeGen\T4CodeGen.csproj` (offline) — clean build from `tools\` only; `T4CodeGen\bin\Debug\T4CodeGen.exe` produced.
- `msbuild T4IntegrationTestBed.sln` — still builds; the new project participates without regressing the task path.

### Manual Checks

1. Run `T4CodeGen.exe` from the test bed project dir with the six required args; confirm `*.t4generated.*` regenerated byte-identical to the task's output on a fresh state and skipped on a no-op.
2. Exit code `0` on success.
3. Deliberately broken template (or an invalid/missing required arg) → non-zero exit and the failing template named in output.
4. Confirm the CLI needs no `Microsoft.Build.*` and no network.

## Risks and Open Questions

- Risk: path shape (relative vs absolute) must match the MSBuild task's to keep manifest/destination markers identical. Mitigation: mirror the task's inputs verbatim (relative paths from the project dir) and verify with a diff.
- Risk: The vendored runtime assembly versions must all be copied beside the exe so it runs outside the build. Mitigation: set local copy / post-build copy of the same `tools\` set the library copies.

## Completion Checklist

- [x] Implementation matches the linked design and goal context
- [x] Scope stayed within this plan
- [x] Verification steps were completed
- [x] Relevant status docs were updated
- [x] Usage doc produced
- [x] No handover needed (Goal 2.2 completes Milestone 2)

## Notes for the Implementing Agent

- The destination/input/template markers in generated files are relative to the project directory; reproduce the task's call exactly, running `T4CodeGen.exe` with the project dir as CWD.
- Reuse `result.TemplateFailures` strings verbatim so CLI error output equals task `Log.LogError` text.
- Keep the CLI a thin mapper — any pipeline fix belongs in `TemplateCompiler.cs`, not the exe.
