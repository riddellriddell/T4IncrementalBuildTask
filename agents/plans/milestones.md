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
| [Milestone 1: Standalone Mono.TextTemplating Engine](#milestone-1) | [goals1.md](goals1.md) | Not Started | Remove the VS/`t4.exe` toolchain dependency so the build task is genuinely standalone and runs in any build pipeline/environment. | Template transformation becomes in-process, portable, and independent of a Visual Studio/MSVC install and `PATH`. |

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

**Status:** Not Started - 0/1 goals complete. See [goals1.md](goals1.md) for details.

**Goals:** [goals1.md](goals1.md)
