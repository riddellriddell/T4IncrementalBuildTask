# Goal 2.1 Implementation Plan: Extract Standalone Compiler API (Task Becomes Thin Wrapper)

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `GOAL`
- Task Name: Goal 2.1 — Extract standalone compiler API
- Status: `Complete`
- Owner: "Your Name"
- Last Updated: `2026-08-31`

## Linked Context

- Design: [design.md](../design.md)
- Milestone: [milestones.md](../milestones.md)
- Goal: [goals2.md](../goals2.md)

## Objective

Move the entire T4 incremental generation pipeline out of the MSBuild `Task` (`BuildT4TextFiles.Execute()`) into a standalone, MSBuild-independent `TemplateCompiler` class in `CustomBuildTasks.dll` (namespace `T4BuildTools`), and rework `BuildT4TextFiles` into a thin adapter so the pipeline is callable from any front-end while `RunCodeGen.targets` keeps working unchanged.

## Problem Summary

The whole pipeline (incremental scan, `.T4ChangedManifest` writes, in-process `Mono.TextTemplating` engine invocation, destination resolution/copy-skip, invalid-file deletion, per-template failure cleanup/continue) lives inside `BuildT4TextFiles.Execute()`, coupled to `ITaskItem`, `TaskLoggingHelper`, and `Log.*`. Nothing outside MSBuild can call, test, or reuse it. Milestone 2 extracts that core into a host-agnostic API.

## Scope

- In scope: new `TemplateCompiler` + `TemplateCompilerResult` classes; rework of `BuildT4TextFiles` into adapter glue; csproj compile-item addition; DOX updates (task contracts, design, plans, goals/milestone status).
- Out of scope: no new T4 features, no retargeting off .NET Framework 4.7.2, no changes to the incremental-scan algorithm / comment-marker contract / destination resolution / failure semantics, no `.exe` CLI (Goal 2.2), no removal of stale `Debug.testproj` / `AddMatchingFilesToOutput`.

## Current State

`BuildT4TextFiles.cs` (549 lines) contains the full pipeline inline plus a private `ProcessTemplateInProcess` that runs the vendored `Mono.TextTemplating` 3.0.0 engine with in-process Roslyn and the three template parameters (`OutputFolder`, `ChangeFileManifest`, `GlobalFileManifest`). `FileScanUtility.cs` provides static scan helpers and is already MSBuild-free.

## Assumptions and Constraints

- The MSBuild task contract is frozen: keep the task's public parameter names (`Name`, `InputFiles`, `T4Templates`, `GeneratedFiles`, `BaseIntermediateOutputPath`, `DefaultFileOutputPath`) and `RunCodeGen.targets` untouched.
- Refactor by move, not rewrite. Carry the pipeline body verbatim; only the logging surface changes (`Log.LogMessage(High, s)` → `Action<string>` sink, `Log.LogError(s)` → collected failures). `Console.WriteLine` calls stay as-is.
- Keep the engine call surface as-is (`ProcessTemplateInProcess`, `AddParameter(null, null, <name>, <value>)`, `ProcessTemplateAsync(...).GetAwaiter().GetResult()`).
- `TemplateCompiler.cs` must compile with no `Microsoft.Build.*` using-directives.

## Files and Areas Likely Affected

- `CustomBuildTasks/BuildT4TextFiles.cs` — becomes thin adapter: pack `ITaskItem[]` to `List<string>`, call `TemplateCompiler.Compile(...)`, forward log lines to `Log.LogMessage`, forward failures to `Log.LogError`, return `result.Success`.
- `CustomBuildTasks/TemplateCompiler.cs` — new standalone API class (moves the `Execute()` body + `ProcessTemplateInProcess`).
- `CustomBuildTasks/CustomBuildTasks.csproj` — add `<Compile Include="TemplateCompiler.cs" />`.
- Docs: `CustomBuildTasks/AGENTS.md`, `agents/plans/design.md`, `agents/plans/goals2.md`, `agents/plans/milestones.md`, `agents/plans/AGENTS.md`, `agents/buildguild.md`, root `AGENTS.md`.

## Implementation Steps

1. Add `TemplateCompiler.cs`: public `TemplateCompilerResult` (`Success`, `TemplateFailures`) and public static `TemplateCompiler.Compile(name, inputFiles, t4Templates, generatedFiles, baseIntermediateOutputPath, defaultFileOutputPath, Action<string> log)`. Move the `Execute()` body verbatim (arrays → `IList<string>` indexing/count), routing `Log.LogMessage(High, s)` to `log(s)` and `Log.LogError(...)` text into `result.TemplateFailures`. Move `ProcessTemplateInProcess` as a private static method. Preserve the early `return false` path (temp-folder create failure) as `result.Success = false`.
2. Rework `BuildT4TextFiles.cs` to adapter glue only (item packing, compile call, log forwarding, failure forwarding, return success flag).
3. Register `TemplateCompiler.cs` in the csproj.
4. Build the library, then the solution; diff the regenerated `*.t4generated.*` outputs against the pre-refactor baseline (prove byte identity).

## Verification Plan

### Automated Checks

- `msbuild CustomBuildTasks.csproj` — clean build; `bin\Debug\CustomBuildTasks.dll` self-contained.
- `msbuild T4IntegrationTestBed.sln` — builds; app runs with expected output.

### Manual Checks

1. No-op rebuild (destinations newer than all inputs): both templates skip; nothing rewritten.
2. Touch a seed (`FancyWrite.h`): only affected outputs regenerate; byte-identical destination writes are skipped; unrelated outputs untouched.
3. Deliberately broken template: fails only that template, its partial output is cleaned from the temp folder, remaining templates continue, MSBuild logs the template error naming the template, task returns `false` → build exit code non-zero.
4. `TemplateCompiler.cs` has no `Microsoft.Build.*` references; `BuildT4TextFiles.cs` contains only adapter glue.

## Completion Checklist

- [x] Implementation matches the linked design and goal context
- [x] Scope stayed within this plan
- [x] Verification steps were completed (see above; all performed 2026-08-31)
- [x] Relevant status docs were updated
- [x] No handover needed (single-phase goal; Goal 2.2 is a separate plan)

## Notes for the Implementing Agent

- The dirty-detection "no-op" loop observed in the test bed (HeaderExample re-running on byte-identical content) is pre-existing algorithm behavior from Goal 1.1 verification timestamps (`FancyWrite.h` newer than its generated file), not a refactor artifact — do not "fix" it in this goal.
- Keep the log-sink mapping 1:1 so CLI output (Goal 2.2) matches task error text.