# T4CodeGenLibrary - Goals (Milestone 2: Standalone Template Compiler API + CLI)

<!-- markdownlint-disable MD001 MD009 MD012 MD022 MD024 MD031 MD032 MD033 MD036 MD040 MD051 MD058 MD060 -->

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

> **Goals and deliverables for Milestone 2: Standalone Template Compiler API + CLI**
>
> **Status:** Not Started
>
> See [milestones.md](milestones.md) for the full milestone roadmap.

---

## Context

- **Problem:** The entire T4 code-generation pipeline (incremental scan, manifest writing, in-process engine invocation, destination copy, per-template failure semantics) is embedded in the MSBuild task `BuildT4TextFiles.Execute()` (`CustomBuildTasks/BuildT4TextFiles.cs`). It cannot be called, tested, or reused outside MSBuild, and the only front-end is `RunCodeGen.targets`' `<BuildT4TextFiles>` task invocation. There is no standalone entry point for scripts, non-MSBuild pipelines, CI steps, or debugging.

## Milestone 2 Goals Summary

| Goal | Status | Link |
|------|--------|------|
| Goal 2.1 - Extract standalone compiler API (task becomes thin wrapper) | Not Started | [Jump to details](#goal-2-1) |
| Goal 2.2 - .exe wrapper front-end on the same API | Not Started | [Jump to details](#goal-2-2) |

**Milestone Status:** Not Started — 0/2 goals complete.

### Milestone 2 Scope

**Intent:** Extract the generation core out of `BuildT4TextFiles` into a standalone, MSBuild-independent compiler class/API, then drive that API from two thin front-ends: the existing build-pipeline MSBuild task and a new command-line `.exe` wrapper.

**What this milestone means:**

- `CustomBuildTasks.dll` exposes a plain public compiler API (plain string/path lists, no `ITaskItem`, no `TaskLoggingHelper`) that runs the same incremental T4 pipeline the task runs today.
- `BuildT4TextFiles` becomes a thin MSBuild adapter over that API; `RunCodeGen.targets` is unchanged.
- A new console `.exe` project references the same dll and runs the same pipeline from the command line, mirroring the task's success/failure semantics.

**Key deliverables:**

- New `TemplateCompiler` class (name TBD in plan) hosting all current `Execute()` pipeline logic, with a pure .NET surface.
- `BuildT4TextFiles` reworked to map MSBuild items/parameters onto the API and forward logged lines/failures to the MSBuild log.
- New console project (e.g. `T4CodeGen.exe`) in the solution, driving the API from CLI args or a config file.

### Explicitly Out of Scope (Milestone 2)

- No new T4 template features beyond those already used in the repo.
- No retargeting off .NET Framework 4.7.2.
- No changes to the incremental-scan algorithm, the comment-marker contract (`T4Gen_TemplateFile` / `T4Gen_InputFile` / `T4Gen_Destination`), destination resolution, or per-template failure semantics.
- No removal/rewrite of the stale `Debug.testproj` harness or the `AddMatchingFilesToOutput` prototype (out of reach of the in-process-engine sweep contract).

---

## Solution Overview

### High-Level Architecture

- **Component 1 (Standalone API):** `TemplateCompiler` in `CustomBuildTasks.dll`, namespace `T4BuildTools`. A plain public class owning the pipeline: dirty-scan, manifest writes, in-process engine run, copy/invalidate. No `Microsoft.Build.*` references; logs and results flow through plain callbacks/result objects.
- **Component 2 (Build-pipeline front-end):** `BuildT4TextFiles` (MSBuild task) — thin adapter: unpack `ITaskItem[]` to path lists, invoke `TemplateCompiler`, forward diagnostics to `Log.*`.
- **Component 3 (CLI front-end):** new console app (e.g. `T4CodeGen`), .NET Framework 4.7.2, references `CustomBuildTasks.dll`, maps CLI args/config onto the same `TemplateCompiler` call.

### Data Flow

#### Flow 1: Build-pipeline front-end (unchanged externally)

1. `RunCodeGen.targets` invokes `<BuildT4TextFiles Name=... T4Templates=... InputFiles=... GeneratedFiles=... DefaultFileOutputPath=... BaseIntermediateOutputPath=... />` — exactly as today.
2. The task packs the MSBuild items/parameters into plain string arguments and calls `TemplateCompiler`.
3. `TemplateCompiler` runs the full pipeline (incremental scan, `.T4ChangedManifest` writes, in-process engine + Roslyn, destination copy, invalid-file deletion) and reports per-file lines + template failures.
4. The task forwards those lines to the MSBuild log and returns the compiler's success flag.

#### Flow 2: CLI front-end

1. User runs `T4CodeGen.exe <inputs/templates/outputs args or config>`.
2. The exe loads the same `CustomBuildTasks.dll`, invokes the same `TemplateCompiler` with the same arguments.
3. The compiler runs the identical pipeline; the exe prints the compiler's log lines to stdout and exits non-zero if any template failed.

---

## Milestone 2 Goals & Deliverables

<a id="goal-2-1"></a>

### Goal 2.1 - Extract standalone compiler API (task becomes thin wrapper)

**Intent:** Move all pipeline logic out of the MSBuild `Task` into a standalone, MSBuild-independent `TemplateCompiler` class, and rework `BuildT4TextFiles` into a thin adapter so the pipeline is callable from any front-end while `RunCodeGen.targets` keeps working unchanged.

#### Deliverables

- **Deliverable 1: Standalone API class**
  - Add `TemplateCompiler` (name to confirm in plan) to `CustomBuildTasks.dll`, namespace `T4BuildTools`, with a pure .NET surface — no `ITaskItem`, no `TaskLoggingHelper`, no `using Microsoft.Build.*`.
  - Public entry takes plain inputs: `Name`, input-file paths, template paths, generated-file paths, `BaseIntermediateOutputPath`, `DefaultFileOutputPath` (as `string`/`IList<string>`), plus a log sink (e.g. `Action<string>` line callback) and returns a result object exposing success + any template failures.
  - Move the entire `Execute()` body (incremental scan, manifest writes, in-process engine invocation via `ProcessTemplateInProcess`, destination resolution, copy-skip, invalid-file deletion, per-template failure cleanup/continue) into the class, unchanged in behavior.

- **Deliverable 2: Thin task adapter**
  - `BuildT4TextFiles.Execute()` is reduced to: map `InputFiles`/`T4Templates`/`GeneratedFiles` item specs and the string parameters onto the API, call `TemplateCompiler`, forward log lines to `Log.LogMessage`/`Log.LogError`, and return the compiler's success flag.
  - Task public parameter names and `RunCodeGen.targets` stay identical; no targets change required.

- **Deliverable 3: Behavior preservation**
  - Incremental state, dirty-set computation, comment-marker contract, `T4Gen_Destination` resolution, byte-identical copy skip, and delete-invalid invalidation behave exactly as before.
  - Per-template failure semantics preserved: partial outputs removed, other templates continue, overall `false` on any failure.

#### Acceptance Criteria

- [ ] `TemplateCompiler` compiles with no `Microsoft.Build.*` references; `BuildT4TextFiles.cs` contains only adapter glue (item packing, `TemplateCompiler` call, log forwarding).
- [ ] `msbuild CustomBuildTasks.csproj` succeeds; `bin\Debug\CustomBuildTasks.dll` still self-contained (vendored engine/Roslyn under `tools\`).
- [ ] Building `T4IntegrationTestBed` regenerates the test bed's `*.t4generated.*` files identically to before (same contents as a pre-refactor run) with no `RunCodeGen.targets` change.
- [ ] Incremental no-op rebuild skips all templates; touching a seed regenerates only the affected outputs; touching no files rewrites nothing.
- [ ] A template that throws fails only that template, cleans its partial outputs, continues the others, and returns `false` for the overall run.

#### Out of Scope

- Any new T4 feature or template surface.
- Fixing the pre-existing solution-build ordering gap (documented in `agents/buildguild.md`).
- Removing the stale `Debug.testproj` / `AddMatchingFilesToOutput` artifacts.

---

<a id="goal-2-2"></a>

### Goal 2.2 - .exe wrapper front-end

**Intent:** Ship a command-line `.exe` front-end that drives the same `TemplateCompiler` API and runs the same incremental pipeline the MSBuild task runs, so the library is usable from scripts, CI, and non-MSBuild pipelines and debuggable standalone.

#### Deliverables

- **Deliverable 1: Console project**
  - New console project (e.g. `T4CodeGen`) in `T4IntegrationTestBed.sln`, classic .NET Framework 4.7.2, referencing `CustomBuildTasks.csproj` (or its `bin\Debug\CustomBuildTasks.dll` output).
  - Builds standalone from the vendored assemblies only (no restore/network), same as the library.

- **Deliverable 2: CLI surface**
  - Accepts the same data the task gets: input files, templates, generated files, `BaseIntermediateOutputPath`, `DefaultFileOutputPath`, from command-line args or a simple config/repsonse file.
  - Invokes `TemplateCompiler` directly; prints the compiler's log lines to stdout.

- **Deliverable 3: Exit-code semantics**
  - Exit `0` on success; non-zero if any template failed (mirrors the task returning `false`).
  - Reports the failed template name in the error output, matching the task's `Log.LogError` text.

- **Deliverable 4: Documentation**
  - Usage doc in the project (or `agents/`) with example invocation against the test bed.

#### Acceptance Criteria

- [ ] `T4CodeGen.exe` run against the test bed's seed files and templates produces the same `*.t4generated.*` outputs as the MSBuild task (diff-equivalent on a full regeneration).
- [ ] Exit code is `0` on a clean incremental run and non-zero when a template fails (verify with a deliberately broken template).
- [ ] Built via `msbuild` with no network access; `tools\` vendored assemblies are the only dependency copies.

#### Out of Scope

- Retargeting the exe or library off .NET Framework 4.7.2.
- Replacing the MSBuild task / `RunCodeGen.targets` (the exe is an additional front-end, not a replacement).
- General T4 full-compatibility beyond what the repo's templates use.

---

## Non-Functional Guarantees

### Standalone / zero external toolchain

- Both front-ends (task and exe) use the same vendored in-process engine + Roslyn compiler under `tools\`; neither requires a Visual Studio/MSVC/`t4.exe` install or a `PATH` entry.

### That the compiler is host-agnostic

- `TemplateCompiler` has no MSBuild dependency, so it can be hosted in the task, the exe, or any future front-end with identical results.
- All diagnostics flow through the single log sink; each front-end renders them through its own channel (MSBuild log vs stdout).

### Behavior preservation

- Incremental scan, dependency markers, destination resolution, content-identical skip, delete-invalid invalidation, and per-template failure isolation are unchanged between the pre- and post-refactor builds.

---

## External Dependencies

### Mono.TextTemplating

- **Version:** 3.0.0 (target)
- **Purpose:** Hostable T4 engine; provides `TemplateGenerator`/`TemplateEngine` and the `Microsoft.VisualStudio.TextTemplating.*` hosting APIs.
- **Status:** Already vendored under `tools\`, referenced via HintPath.
- **Note:** Not a new dependency — moved with the pipeline logic into `TemplateCompiler`.

### Mono.TextTemplating.Roslyn

- **Version:** 3.0.0 (target)
- **Purpose:** Bundles the Roslyn C# compiler for in-process template compilation (`UseInProcessCompiler()`).
- **Status:** Already vendored under `tools\`, referenced via HintPath.

---

## Agent Guidelines for This Milestone

When working on tasks in this milestone, anchor them to the goals above.

### Project Boundaries

- API extraction + task rework: `CustomBuildTasks/` (`BuildT4TextFiles.cs`, new `TemplateCompiler.cs`, `CustomBuildTasks.csproj`, `CustomBuildTasks/AGENTS.md`).
- CLI front-end: new console project under the solution root (e.g. `T4CodeGen/`) + `T4IntegrationTestBed.sln`.
- Docs: `agents/plans/design.md`, `agents/plans/milestones.md`, `agents/plans/goals2.md`, `agents/plans/AGENTS.md`, `CustomBuildTasks/AGENTS.md`.

### Development Approach

- Refactor by move, not rewrite: carry the `Execute()` pipeline logic into the API class verbatim, then delete adapter-only MSBuild sugar. Diff the test bed's generated files before/after to prove identity.
- Keep the task's public parameters and `RunCodeGen.targets` untouched so the MSBuild contract is stable across the refactor.
- Keep the engine call as-is (`ProcessTemplateInProcess`, `AddParameter(null, null, name, value)`, `ProcessTemplateAsync(...).GetAwaiter().GetResult()`); do not re-shape the Mono.TextTemplating surface in this milestone.
- For the exe, prefer args/config that map 1:1 onto the compiler's public parameters; reuse the existing per-template failure messages so CLI output matches task error text.

### Rules Compliance

- Follow the local contracts in `CustomBuildTasks/AGENTS.md`, updating it where the task's described internals change (task-as-adapter, new `TemplateCompiler.cs` contract).
- Update `agents/plans/design.md` if wording about the task being the whole pipeline becomes stale after 2.1.
- Follow the build/test recipe in `agents/buildguild.md` (Verification Bar) for every change.

---

## Related Documents

- **`milestones.md`** - Authoritative milestone definitions
- **`design.md`** - Technical architecture specification
- **`implementation plans/`** - Tier-4 implementation plans for Goal 2.1/2.2 (created per deliverable during this milestone)
- **`CustomBuildTasks/AGENTS.md`** - Build task local contracts
- **`agents/buildguild.md`** - Canonical build/test recipe and Verification Bar