# T4CodeGenLibrary – Milestones

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

> Authoritative milestone definitions for the project.
>
> Related: [design.md](design.md)

---

## Table of Contents

| Milestone | Goals File | Status | Why | Impact |
| --- | --- | --- | --- | --- |
| [Milestone 1: Standalone Mono.TextTemplating Engine](#milestone-1) | [goals1.md](goals1.md) | Complete | Remove the VS/`t4.exe` toolchain dependency so the build task is genuinely standalone and runs in any build pipeline/environment. | Template transformation becomes in-process, portable, and independent of a Visual Studio/MSVC install and `PATH`. |
| [Milestone 2: Standalone Template Compiler API + CLI](#milestone-2) | [goals2.md](goals2.md) | Not Started | The whole pipeline is baked into the MSBuild `Task`, so it can't be called outside MSBuild; a standalone API plus a CLI gives scripts/CI/non-MSBuild pipelines the same incremental codegen. | Generation core becomes host-agnostic: exposed as a pure .NET API, kept working through the MSBuild task, and driven by a new `.exe` front-end. |

---

<a id="milestone-1"></a>

## Milestone 1: Standalone Mono.TextTemplating Engine

**Intent:** Replace the VS-installed `t4.exe` shell-out in the build task with an in-process Mono.TextTemplating engine (with in-process Roslyn compiler) so generating code from `.tt` templates depends on no external toolchain install.

**Why it matters:**

- The component is designed to be standalone but today depends on a Visual Studio/MSVC installation and `t4` on `PATH` (`design.md` "Standalone Intent").
- Build pipelines without that install cannot run the template transformation.
- The in-process Roslyn compiler removes the external C# toolchain dependency too, and fixes the misspelled `ChangeFileMainfest` parameter along the way.

**Impact:**

- Template generation runs entirely in-process with no process spawn and no `PATH` requirement.
- The task works in fresh/CI environments that lack Visual Studio's T4 tooling.
- Incremental behavior, dependency markers, destination resolution, and copy semantics are preserved.

**Status:** Complete - 1/1 goals complete. Goal 1.1 all deliverables landed 2026-08-31 (in-process engine + vendored Roslyn under `tools\`, parameter rename, debug preserved, per-template failure semantics). See [goals1.md](goals1.md) for details.

**Goals:** [goals1.md](goals1.md)

---

<a id="milestone-2"></a>

## Milestone 2: Standalone Template Compiler API + CLI

**Intent:** Extract the T4 generation core from the MSBuild `Task` into a standalone, MSBuild-independent compiler class/API, then drive that API from two thin front-ends: the existing build-pipeline task and a new `.exe` wrapper.

**Why it matters:**

- The entire incremental pipeline (scan, manifests, engine run, copy) lives inside `BuildT4TextFiles.Execute()`, callable only from MSBuild.
- Scripts, CI steps, and non-MSBuild pipelines have no entry point to the same codegen, and there is no standalone way to debug the pipeline.
- A pure .NET API makes the core host-agnostic and testable in isolation; the `.exe` gives a CLI front-end that mirrors the task's semantics.

**Impact:**

- `CustomBuildTasks.dll` exposes a standalone compiler API with no `ITaskItem`/MSBuild dependencies; the task becomes a thin adapter (`RunCodeGen.targets` untouched).
- A new console project drives the same incremental pipeline from the command line, with matching output and exit-code/failure semantics.
- Incremental behavior, dependency markers, destination resolution, and failure isolation are preserved exactly.

**Status:** Not Started - 0/2 goals complete. See [goals2.md](goals2.md) for details.

**Goals:** [goals2.md](goals2.md)
