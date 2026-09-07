# Implementation Plan: Relocate Vendored `tools/` Under `T4CodeGen/`

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `FEATURE`
- Task Name: Relocate vendored `tools/` under `T4CodeGen/`
- Status: `Draft`
- Owner: "Your Name"
- Last Updated: `2026-09-07`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [buildguild.md](../../buildguild.md)
- Milestone: [milestones.md](../milestones.md) (post-Milestone-2 hardening of the exe's deployment story)
- Goal: [goals2.md](../goals2.md) (Goal 2.2 `T4CodeGen.exe`; this plan fixes its vendor-boundary scope)

## Objective

Relocate the vendored third-party assembly tree from the repo root `tools/` to `T4CodeGen/tools/` so the `T4CodeGen.exe` drop-in/deployment boundary is the single project folder `T4CodeGen/` instead of the whole 12-package repo-root tree. A consumer must be able to copy one folder and run the exe standalone. Both HintPath consumers — the `CustomBuildTasks` MSBuild-task library and the CLI — keep building offline from the relocated tree, and the MSBuild task keeps working unchanged.

## Problem Summary

The CLI bundles its engine purely through compile-time HintPaths into a repo-**root**-level vendor tree: `T4CodeGen/T4CodeGen.csproj` and `CustomBuildTasks/CustomBuildTasks.csproj` both point `$(MSBuildThisFileDirectory)..\tools\<package>\<version>\...`, and `CustomBuildTasks.csproj` is rooted there too. The vendored dependency boundary is therefore repo-root-scaled, not project-scaled: a consumer who only wants the CLI must copy the whole 12-package `tools/` tree (5 engine/compiler assemblies + 7 `System.*` runtime deps) alongside the exe. The messy part of the otherwise-deliberate vendoring decision (see `GOAL_1_1_host-engine-in-process.md`, "Vendoring instead of PackageReference") is the boundary's location. Moving the tree under `T4CodeGen/` makes the copy boundary one folder; keeping `CustomBuildTasks` reachable to the same tree keeps the task path (the primary product) fully working.

Two sub-approaches were weighed:

- **(a) Move the tree and rewire HintPaths** — relocate `tools/` to `T4CodeGen/tools/`, point `T4CodeGen.csproj` at its sibling `tools\`, and point `CustomBuildTasks.csproj` up-and-over to `..\T4CodeGen\tools\`. Single source of truth, no byte duplication, both consumers keep building offline. **Recommended.**
- **(b) Ship only the referenced subset in the release zip** — a packaging scheme (which DLLs the exe *actually* loads at runtime, not the compile-time reference set) that belongs to the separate GitHub Releases plan. This plan records it as a documented decision and the empirical derivation approach, but implements no zip logic.

## Scope

- In scope: physically move the `tools/` tree (12 packages, `package\version` layout, license files) to `T4CodeGen/tools/` via `git mv`, preserving content.
- In scope: rewire the 12 HintPath references in `T4CodeGen/T4CodeGen.csproj` from `$(MSBuildThisFileDirectory)..\tools\` to `$(MSBuildThisFileDirectory)tools\` (plus the "See tools/ in the repo root" comment).
- In scope: rewire the 12 HintPath references in `CustomBuildTasks/CustomBuildTasks.csproj` from `$(MSBuildThisFileDirectory)..\tools\` to `$(MSBuildThisFileDirectory)..\T4CodeGen\tools\` so the task library stays buildable and its `bin\Debug` keeps carrying the engine/runtime set.
- In scope: update `.gitignore` whitelist from `/tools/` + `/tools/**` to `/T4CodeGen/tools/` + `/T4CodeGen/tools/**` (must land before the move so the relocated binaries stay tracked against the machine-global `*.dll` ignore rule).
- In scope: DOX/doc pass — root `AGENTS.md`, `T4CodeGen/AGENTS.md`, `CustomBuildTasks/AGENTS.md`, `agents/plans/AGENTS.md` index, `agents/buildguild.md`, `T4CodeGen/README.md`, `agents/plans/design.md`.
- In scope: record the (b) "ship only the runtime-referenced subset" scheme as the packaging contract for the future Releases plan, plus how to derive that subset empirically.
- Out of scope: GitHub Releases/zip artifact generation (separate plan).
- Out of scope: making the exe source self-contained by moving `TemplateCompiler.cs`/`FileScanUtility.cs` into `T4CodeGen/` (separate plan). If that plan lands first, this plan simplifies to `T4CodeGen.csproj` + docs only — sequencing is covered in Assumptions.
- Out of scope: changing the `TemplateCompiler.Compile` API, the `BuildT4TextFiles` task contract, `RunCodeGen.targets`/`RunCodeGen.xml`, the exe output path, or the `T4CodeGenTests` harness contract.

## Current State

- `tools/` (repo root) holds 12 vendored packages, one folder per `package\version`, with license files:
  - `microsoft.codeanalysis.common\4.7.0\`, `microsoft.codeanalysis.csharp\4.7.0\` (Roslyn in-process compiler),
  - `mono.texttemplating\3.0.0\`, `mono.texttemplating.roslyn\3.0.0\` (engine + Roslyn host),
  - `system.buffers\4.5.1\`, `system.collections.immutable\7.0.0\`, `system.memory\4.5.5\`, `system.numerics.vectors\4.5.0\`, `system.reflection.metadata\7.0.0\`, `system.runtime.compilerservices.unsafe\6.0.0\`, `system.text.encoding.codepages\7.0.0\`, `system.threading.tasks.extensions\4.5.4\` (.NET 4.7.2 runtime deps).
- Two HintPath consumers, both via `$(MSBuildThisFileDirectory)..\tools\<package>\<version>\` (12 `<Reference>` entries each): `CustomBuildTasks/CustomBuildTasks.csproj` (lines 57–94, comment "See tools/ in the repo root") and `T4CodeGen/T4CodeGen.csproj` (lines 56–95, comment "Mirrors the library's HintPath set under tools/"). Assembly CopyLocal is default, so `CustomBuildTasks\bin\Debug\` and `T4CodeGen\bin\Debug\` each carry a runtime copy of the referenced set beside `CustomBuildTasks.dll` / `T4CodeGen.exe`.
- `.gitignore` lines 334–338 whitelist the vendored binaries with `!/tools/` + `!/tools/**`, overriding the machine-global `*.dll` ignore rule so the committed DLLs stay tracked.
- `T4CodeGenTests/Program.cs` locates only `T4CodeGen\bin\Debug\T4CodeGen.exe` by path relative to the repo root and spawns it against a fixture scratch workspace; it never touches `tools/`, so the relocation does not change the harness's exe path or behavior.
- Grep confirms no other repo file (`.targets`, `.xml`, `.bat`, `.md`, `.cs`) references `tools\` beyond the two csprojs and the docs listed in Scope; `RunCodeGen.targets` loads `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` by path and never references the vendored tree directly.

## Assumptions and Constraints

- Offline, zero-NuGet build from vendored DLLs only — no `PackageReference`, no `packages.config`, no restore. HintPaths must continue resolving with no network.
- The MSBuild-task library is a first-class consumer of the same tree; the assemblies cannot simply "vanish" from a location `CustomBuildTasks.csproj` can reach, or step 1 of the canonical flow (`msbuild CustomBuildTasks.csproj`) breaks.
- CopyLocal is part of the contract: `CustomBuildTasks\bin\Debug\` carries the engine/Roslyn set (MSBuild loads the task in-proc and needs the engines beside the DLL), and `T4CodeGen\bin\Debug\` carries the same set beside the exe so it runs outside the build.
- `.gitignore` must keep whitelisting the binaries at their new location; the moved files must remain fully tracked.
- Sequencing with the separate self-contained-project plan: this plan can land either before or after it. Recommended order: self-contained plan **after** this one, so this plan's `CustomBuildTasks.csproj` HintPath rewrite is the interim wiring that the self-contained plan deletes when `TemplateCompiler.cs`/`FileScanUtility.cs` move into `T4CodeGen/`. If self-contained lands first, skip the `CustomBuildTasks.csproj` step (no references remain).
- Sequencing with the separate GitHub Releases plan: after this plan, the artifact boundary is `T4CodeGen\`; the Releases plan packages that folder (or the runtime-referenced subset — sub-approach (b)), copying from one project-local tree instead of the repo root.
- The "runtime-referenced subset" is empirical, not equal to the 12-entry compile-time reference set: `.NET Framework 4.7.2` already ships some `System.*` types in the GAC. The subset is determined by running the exe from a stripped copy (see Verification) — that work belongs to the Releases plan.
- `T4CodeGen.exe`, `test.bat`, and the test harness paths are unchanged; no fixture or harness edits are required.

## Files and Areas Likely Affected

- `tools/` → `T4CodeGen/tools/` — the physical relocation of the 12-package vendored tree (git mv, content preserved).
- `T4CodeGen/T4CodeGen.csproj` — 12 HintPath rewrites `..\tools\` → `tools\`; comment "See tools/ in the repo root" → "See tools/ under this project".
- `CustomBuildTasks/CustomBuildTasks.csproj` — 12 HintPath rewrites `..\tools\` → `..\T4CodeGen\tools\`; comment stays accurate about the new location.
- `.gitignore` — whitelist lines 337–338 (`!/tools/`, `!/tools/**`) → `/T4CodeGen/tools/` entries; comment lines 334–336 updated.
- `AGENTS.md` (root) — Child DOX Index: `T4CodeGen/` entry gains the `tools/` subtree; the root-owned `tools/` entry moves under `T4CodeGen/`; line 8 "vendored under `tools/`" updated.
- `T4CodeGen/AGENTS.md` — Local Contracts + Verification: `..\tools\<package>\<version>\` → project-local `tools\<package>\<version>\`; "from `tools\` only" wording follows the new path.
- `CustomBuildTasks/AGENTS.md` — Local Contracts + Verification: the `..\tools\` HintPath contract updated to the new referenced location.
- `T4CodeGen/README.md` — "Builds standalone from the vendored `tools\` assemblies" updated to name the new location.
- `agents/buildguild.md` — three `tools\` mentions (status banner line 9, prerequisites line 15, gotcha line 51) updated to `T4CodeGen\tools\`.
- `agents/plans/design.md` — "vendored under `tools\`" (line 22) updated to the new location.
- `agents/plans/AGENTS.md` — implementation-plans index gains a line for this plan; the `GOAL_1_1` history line is left as-is (it records the original decision).
- `T4CodeGenTests/` — NOT affected: reference only to note it locates the exe, not the DLLs.

## Implementation Steps

1. Update `.gitignore` first: replace `/tools/` + `/tools/**` with `/T4CodeGen/tools/` + `/T4CodeGen/tools/**` (and update the explanatory comment) before any filesystem move, so the relocated binaries are never invisible to git.
2. `git mv tools T4CodeGen/tools` (from the repo root). Confirm all 12 package folders plus their license files moved, nothing remains under a root `tools/`, and `git status` shows only renames (no deletions of tracked files).
3. Rewire `T4CodeGen/T4CodeGen.csproj`: change all 12 HintPaths from `$(MSBuildThisFileDirectory)..\tools\` to `$(MSBuildThisFileDirectory)tools\` and update the reference comment to drop "repo root".
4. Rewire `CustomBuildTasks/CustomBuildTasks.csproj`: change all 12 HintPaths from `$(MSBuildThisFileDirectory)..\tools\` to `$(MSBuildThisFileDirectory)..\T4CodeGen\tools\` and update the comment.
5. Grep-sanity: no remaining `..\tools\` HintPath in either csproj; no remaining root-anchored `tools/` reference anywhere.
6. DOX pass: update the root `AGENTS.md` index (move the `tools/` entry under `T4CodeGen/`, refresh the `T4CodeGen/` entry, fix line 8), then `T4CodeGen/AGENTS.md`, `CustomBuildTasks/AGENTS.md`, `T4CodeGen/README.md`, `agents/buildguild.md`, `agents/plans/design.md`, and add this plan's index line to `agents/plans/AGENTS.md`.
7. Record the (b) Release-ship-subset decision: a short note (in the release artifact plan, once it exists, else here) that the zip copies from `T4CodeGen\tools\` and ships only the DLLs the exe demonstrably loads at runtime (verified by the stripped-copy check below), not the full 12-package reference set.

## Verification Plan

### Automated Checks

- `msbuild CustomBuildTasks.csproj` (offline) — succeeds with the relocated HintPaths; `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` exists and `bin\Debug\` carries `Mono.TextTemplating.dll`, `Mono.TextTemplating.Roslyn.dll`, `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll`, and the `System.*` runtime set.
- `msbuild T4CodeGen\T4CodeGen.csproj` (offline) — succeeds; `T4CodeGen\bin\Debug\T4CodeGen.exe` produced with the engine/Roslyn runtime DLLs copied beside it from `T4CodeGen\tools\` only.
- `test.bat` — builds library + exe + harness and runs the case battery; must print `PASS` for every case and exit `0` (proves the exe's full contract survived the relocation).
- `msbuild T4IntegrationTestBed.sln` — the task path (the primary consumer) still runs `GenerateT4Files` in-process and regenerates the test bed's `*.t4generated.*` files.
- `git check-ignore -v T4CodeGen/tools/mono.texttemplating/3.0.0/Mono.TextTemplating.dll` — must show the `!` negation from the new whitelist line (i.e. the DLL is not ignored).
- `git status` / `git ls-files T4CodeGen/tools | Measure-Object` — the 12-package tree is fully tracked under `T4CodeGen/tools/` and there is no root `tools/`.

### Manual Checks

1. Copy the `T4CodeGen\` folder (bin + `tools\`) to a scratch directory outside the repo, run `T4CodeGen.exe` from a scratch workdir with the test-bed-style relative inputs (per `T4CodeGen/README.md`'s worked example), and confirm exit `0`, correct generated content, and no "assembly not found" errors. **This is the payoff test: the one-folder drop-in boundary works.**
2. Runtime-subset probe for the future Releases plan: from the scratch copy, delete one candidate DLL at a time (starting with the `System.*` deps) and re-run the exe; the set that must remain for green runs is the "referenced subset" the zip plan ships.

## Risks and Open Questions

- Risk: the `.gitignore` whitelist must be rewritten before the `git mv`, or the relocated DLLs fall under the machine-global `*.dll` ignore and look untracked/ignored in `git status`. Mitigation: step 1 runs first and step 5 verifies.
- Risk: `CustomBuildTasks` now depends into `T4CodeGen/tools\`, which is an ownership oddity (task references the CLI's folder). Mitigation: documented interim wiring; the separate self-contained-project plan deletes the library's tools references entirely, after which only `T4CodeGen/` owns the tree.
- Risk: physical move vs. copy duplication. A copy would leave the root tree for the library *and* a `T4CodeGen\tools\` for the exe — roughly doubling the vendored bytes and adding a second whitelist. Decision (now): single moved tree shared by both, per the change statement; revisit only if the self-contained plan re-sequences first.
- Question: should `CustomBuildTasks` instead keep a root-level shared location (e.g. `T4CodeGen\tools\` is still one-hop from root) rather than reference into the CLI project's folder? Decision (now): yes, reference `..\T4CodeGen\tools\`; it is the minimal change that keeps one tree, and it dissolves when the self-contained plan lands.
- Risk: path length grows by one segment (`T4CodeGen\` prefix) for the vendored files. Negligible at this repo depth; no `MAX_PATH` exposure inside the source tree.
- Question: are all 12 packages genuinely needed at runtime, or is the compile-time set larger than the runtime set (e.g. GAC-provided `System.*`)? Answer (now): the runtime subset is empirical and becomes the Releases plan's packaging input (step 7); no behavioral change today.
- Dependency: the `msbuild` commands require the VS2022 toolchain from `buildguild.md`; no external/network dependency is introduced.

## Completion Checklist

- [ ] Implementation matches the linked design and goal context
- [ ] Scope stayed within this plan
- [ ] Verification steps were completed or explicitly deferred
- [ ] Relevant status docs were updated
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Read the DOX chain before editing: root `AGENTS.md` → `agents/AGENTS.md` → `agents/plans/AGENTS.md`, plus `CustomBuildTasks/AGENTS.md`, `T4CodeGen/AGENTS.md`, and `T4CodeGenTests/AGENTS.md` (the latter only to confirm it is unaffected).
- Order matters in two places: `.gitignore` before `git mv`; library rebuild before any solution build (root `RunCodeGen.targets` hardcodes `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll`).
- Keep the `package\version` layout and license files byte-identical during the move — do not re-flatten or rename anything.
- Do not touch `T4CodeGenTests/` or `RunCodeGen.targets`; grep for `..\tools\` after the rewires and expect zero matches outside the two csprojs' comments (which you are updating anyway).
- The relocation only moves files and rewires references; if a HintPath rewrite exposes a build error, it is a bad-path bug in this plan, not a signal to revert to repo-root tools.