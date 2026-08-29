# T4CodeGenLibrary - Goals (Milestone 1: Standalone Mono.TextTemplating Engine)

<!-- markdownlint-disable MD001 MD009 MD012 MD022 MD024 MD031 MD032 MD033 MD036 MD040 MD051 MD058 MD060 -->

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

> **Goals and deliverables for Milestone 1: Standalone Mono.TextTemplating Engine**
>
> **Status:** Not Started
>
> See [milestones.md](milestones.md) for the full milestone roadmap.

---

## Context

- **Problem:** The build task shells out to the VS-installed `t4.exe` (via `powershell.exe`), so running templates requires a Visual Studio/MSVC installation and `t4` on `PATH`. This prevents the component from being genuinely standalone and from running in build pipelines/environments without that install.

## Milestone 1 Goals Summary

| Goal | Status | Link |
|------|--------|------|
| Goal 1.1 - Host Mono.TextTemplating in-process (remove t4.exe shell-out & PATH) | Not Started | [Jump to details](#goal-1-1) |

**Milestone Status:** Not Started - 0/1 goals complete.

### Milestone 1 Scope

**Intent:** Replace the VS-installed `t4.exe` shell-out with an in-process Mono.TextTemplating engine so the build task depends on no external toolchain install for template transformation.

**What this milestone means:**

- The build task generates code from `.tt` templates entirely in-process, with no `t4` on `PATH` and no MSVC/VS dependency for the T4 step.
- Templates compile and run using the bundled in-process Roslyn compiler.
- All existing incremental behavior, dependency markers, and copy semantics are preserved.

**Key deliverables:**

- `CustomBuildTasks` references `Mono.TextTemplating` (+ `Mono.TextTemplating.Roslyn`) and hosts the engine inside `BuildT4TextFiles`.
- The `powershell.exe` + `t4 -p=...` invocation path is removed.
- The misspelled template parameter (`ChangeFileMainfest`) is renamed and fixed in all templates.

### Explicitly Out of Scope (Milestone 1)

- No new T4 template features beyond those already used in the repo.
- No general VS T4 full-compatibility guarantee.
- No retargeting away from .NET Framework 4.7.2 (kept unless verification proves Mono.TextTemplating is incompatible — then revisit).
- No changes to the incremental-scan algorithm or the comment-marker contract.

---

## Solution Overview

### High-Level Architecture

- **Component 1 (Hosted engine):** `TemplateGenerator` (or subclass) from Mono.TextTemplating, hosted directly in `BuildT4TextFiles.Execute()`.
- **Component 2 (In-process compiler):** `Mono.TextTemplating.Roslyn` via `UseInProcessCompiler()` so template compilation needs no external C# compiler.
- **Component 3 (Parameter passing):** the three string parameters flow into the engine via the session/host parameter mechanism, matching the current `<#@ parameter #>` directives in the templates.

### Data Flow

#### Flow 1: Template transformation (replacing the shell-out)

1. Task identifies per-template dirty files and writes `<TemplateName>.T4ChangedManifest` (unchanged logic).
2. Task invokes the in-process engine on `<template>.tt` with `OutputFolder`, `ChangeFileManifest`, and `GlobalFileManifest` parameter values.
3. Engine compiles the template (in-process Roslyn; `debug="true"` honored) and executes it.
4. Generated files land in `BaseIntermediateOutputPath\GeneratedFiles\`; the existing copy/skip/destination logic then runs unchanged.

---

## Milestone 1 Goals & Deliverables

<a id="goal-1-1"></a>

### Goal 1.1 - Host Mono.TextTemplating in-process (remove t4.exe shell-out & PATH)

**Intent:** Swap the `t4.exe` + `powershell.exe` shell-out in `BuildT4TextFiles` for an in-process Mono.TextTemplating engine (with in-process Roslyn compiler) so the build no longer depends on any external MSVC/VS/`t4` install or a `PATH` value.

#### Deliverables

- **Deliverable 1: In-process engine integration**
  - Add `Mono.TextTemplating` (and `Mono.TextTemplating.Roslyn`) package reference(s) to `CustomBuildTasks.csproj`, working under the existing .NET Framework 4.7.2 target.
  - Replace the `ProcessStartInfo` / `powershell.exe` / `t4 -p=...` invocation (currently `BuildT4TextFiles.cs` around lines 316-393) with a direct engine call (`TemplateGenerator` / `TemplateEngine` + `UseInProcessCompiler()`), removing the process spawn, the `t4` command line, and the requirement for `t4` on `PATH`.
  - Pass the template parameters (`OutputFolder`, `ChangeFileManifest`, `GlobalFileManifest`) through the engine's parameter/session mechanism so the `<#@ parameter #>` directives in the templates keep working.

- **Deliverable 2: Rename the misspelled parameter**
  - Rename `ChangeFileMainfest` to `ChangeFileManifest` in the task's parameter passing in `BuildT4TextFiles.cs`.
  - Update the `<#@ parameter #>` declaration and all usages in `T4IntegrationTestBed\T4Templates\HeaderExample.tt` and `TestTemplate.tt` (and any `ttinclude` references).
  - Preserve the other two parameter names and their values exactly.

- **Deliverable 3: Debugging preserved**
  - Honor the existing `debug="true"` template directive so generated template code remains debuggable (as today).
  - Keep the template feature surface as-is; no new directives or features are introduced.

- **Deliverable 4: Failure semantics (clean up + continue + fail)**
  - Maintain the existing clean-up of the temporary `GeneratedFiles` folder before generation.
  - On a per-template failure: clean up that template's partial outputs, log a clear error, and continue processing the remaining `.tt` files.
  - Return `false` from the task if any template failed, so the build fails after attempting all templates.

#### Acceptance Criteria

- [ ] `CustomBuildTasks.csproj` builds on .NET Framework 4.7.2 with the Mono.TextTemplating reference(s) with no errors.
- [ ] `BuildT4TextFiles.cs` no longer references `powershell.exe` or a `t4` command; there is no `t4` PATH requirement.
- [ ] Building `T4IntegrationTestBed.sln` regenerates the test bed's `*.t4generated.*` files (incremental dirty detection still works) without `t4.exe` installed / on PATH.
- [ ] `ChangeFileMainfest` is renamed to `ChangeFileManifest` everywhere (task + all `.tt`/`.ttinclude` files); no stale references to the old name remain.
- [ ] A template that throws fails only that template, cleans up its partial outputs, continues the others, and causes the overall task to return `false`.
- [ ] Existing dependency markers (`T4Gen_TemplateFile`, `T4Gen_InputFile`, `T4Gen_Destination`) and the skip-identical-file copy behavior are unchanged.

#### Out of Scope

- Any T4 feature not already used by the repo's templates.
- Retargeting the library off .NET Framework 4.7.2 (unless verification requires it, tracked separately).
- Rewriting/debugging the incremental-scan algorithm.

---

## Non-Functional Guarantees

### Standalone / zero external toolchain

- Template transformation depends on no MSVC, Visual Studio, or `t4.exe` install and no `PATH` entry; the bundled engine and in-process Roslyn compiler provide everything needed.

### Failure isolation

- A failing template does not abort the remaining templates; partial outputs for the failed template are removed so a failed build does not leave stale generated files.

### Behavior preservation

- Parameter passing, dependency tracking, destination resolution, content-identical skip, and delete-invalid invalidation behave as before the swap.

---

## External Dependencies

### Mono.TextTemplating

- **Version:** 3.0.0 (target)
- **Purpose:** Hostable reimplementation of the VS T4 engine; provides `TemplateGenerator`/`TemplateEngine` and the `Microsoft.VisualStudio.TextTemplating.*` hosting APIs.
- **Status:** Active

### Mono.TextTemplating.Roslyn

- **Version:** 3.0.0 (target)
- **Purpose:** Bundles the Roslyn C# compiler for in-process template compilation (`UseInProcessCompiler()`).
- **Status:** Active

---

## Agent Guidelines for This Milestone

When working on tasks in this milestone, anchor them to the goal above.

### Project Boundaries

- Engine integration: `CustomBuildTasks/` (`BuildT4TextFiles.cs`, `CustomBuildTasks.csproj`).
- Parameter rename: `CustomBuildTasks/BuildT4TextFiles.cs` + `T4IntegrationTestBed/T4Templates/*.tt` (+ `*.ttinclude`).

### Development Approach

- Keep the engine swap behavior-identical: preserve all incremental logic, parameter names (except the typo fix), markers, and copy semantics.
- Prefer the high-level `TemplateGenerator` API unless finer control (e.g. `TemplateSettings.CompilerOptions`) is needed.
- Use the in-process Roslyn compiler so no external C# toolchain is required.

### Rules Compliance

- Follow the local contracts in `CustomBuildTasks/AGENTS.md` and `T4IntegrationTestBed/T4Templates/AGENTS.md`, updating those docs where behavior contracts change (e.g. the parameter rename).
- Update `agents/plans/design.md` if wording about the `t4.exe` / PATH dependency becomes stale after the swap.

---

## Related Documents

- **`milestones.md`** - Authoritative milestone definitions
- **`design.md`** - Technical architecture specification
- **`implementation plans/`** - Tier-4 implementation plans for Goal 1.1 (one per deliverable): `GOAL_1_1_host-engine-in-process.md`, `GOAL_1_1_rename-change-file-mainfest-parameter.md`, `GOAL_1_1_preserve-debugging.md`, `GOAL_1_1_failure-semantics.md`
- **`CustomBuildTasks/AGENTS.md`** - Build task local contracts
- **`T4IntegrationTestBed/T4Templates/AGENTS.md`** - Template parameter and marker conventions
