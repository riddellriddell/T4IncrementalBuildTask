# plans

## Purpose

Planning and design documents for this repository. Holds the high-level design (`design.md`) and the implementation plans that break it into buildable work.

## Ownership

- Owned by this folder.
- Read by agents planning or implementing changes to the codebase.

## Local Contracts

- `design.md` — project overview, core technology, standalone intent, and pipeline role. Authoritative description of what the project is for.
- `milestones.md` — authoritative milestone definitions (roadmap) at the same tier as the design; each milestone links to its goals file (`goalsN.md`).
- `goalsN.md` — tier-3 goals files, one per milestone, containing per-goal deliverables and acceptance criteria. `goals1.md` = Milestone 1 (Standalone Mono.TextTemplating Engine); `goals2.md` = Milestone 2 (Standalone Template Compiler API + CLI).
- `implementation plans/` — concrete, sequenced implementation plans (tier-4), derived from the design/goals; only implementation plans live in this subfolder. Current set (Goal 1.1, one plan per deliverable, see `goals1.md`):
  - `GOAL_1_1_host-engine-in-process.md` — Deliverable 1: swap the `t4.exe`/`powershell.exe` shell-out for an in-process Mono.TextTemplating engine (+ Roslyn), keeping parameters, markers, and copy semantics. **Landed 2026-08-30** (vendored under `tools\`, folded Deliverables 2-3).
  - `GOAL_1_1_rename-change-file-mainfest-parameter.md` — Deliverable 2: rename `ChangeFileMainfest` -> `ChangeFileManifest` in task and templates. **Superseded** — folded into the host-engine plan (landed).
  - `GOAL_1_1_preserve-debugging.md` — Deliverable 3: keep `debug="true"` template debugging working after the swap.
  - `GOAL_1_1_failure-semantics.md` — Deliverable 4: per-template failure = clean partial outputs + log error + continue + return `false`. **Landed 2026-08-31** (Goal 1.1 complete).
  - Milestone 2 plans (Goal 2.1/2.2) — symmetry implementation plans, one per deliverable. `GOAL_2_1_extract-standalone-compiler-api.md` (**Landed 2026-08-31**, Goal 2.1 complete); `GOAL_2_2_cli-exe-front-end.md` (**Landed 2026-09-03**, Goal 2.2 + Milestone 2 complete). See `goals2.md`.
  - `FEATURE_exe-test-project.md` — automated, offline, zero-NuGet black-box test harness for `T4CodeGen.exe` (`T4CodeGenTests/` console project), covering exit codes, response files, list separators, byte-identical incremental regeneration, and per-template failure isolation. **Landed 2026-09-05.**
  - `FEATURE_wildcard-path-support.md` — host-agnostic wildcard (`*`/`?`/`**`/`[]`) expansion of the three path lists (`InputFiles`/`T4Templates`/`GeneratedFiles`) in `TemplateCompiler.Compile` via a new `CustomBuildTasks/PathExpander.cs` (task path stays a no-op), declarative `TextTemplateFile` glob in the test bed vcxproj, CLI usage/README docs, and five new black-box cases. **Draft 2026-09-05.**
  - Vendoring/consumption plan set (**all Draft 2026-09-07**), targeting making the standalone `T4CodeGen.exe` easy to vendor into other projects (see `T4CodeGen/README.md`, root `AGENTS.md`):
    - `RELEASE_prebuilt-artifact-github-releases.md` — publish a self-contained release zip (`T4CodeGen.exe` + engine/Roslyn runtime DLLs) via GitHub Releases with semver tags, so consumers fetch a prebuilt asset instead of vendoring source (a new `scripts\package-release.ps1` + optional `.github\workflows\release.yml`). **Highest payoff.**
    - `FEATURE_self-contained-t4codegen-project.md` — make `T4CodeGen/` one self-contained csproj: move `TemplateCompiler.cs` + `FileScanUtility.cs` into the project (dropping the `ProjectReference` to `CustomBuildTasks`), with `CustomBuildTasks.csproj` compiling them via `<Compile Include Link>` so the task DLL is unchanged. **Highest payoff.**
    - `FEATURE_relocate-tools-under-t4codegen.md` — physically move the vendored 12-package `tools/` tree to `T4CodeGen/tools/` and rewire both csproj HintPaths, so the exe's drop-in boundary is project-level not repo-root-level (`.gitignore` whitelist updated first). **Highest payoff.**
    - `FEATURE_semantic-tags-release-notes.md` — establish a semver (`vX.Y.Z`) tagging convention + root `CHANGELOG.md`, and document that consumers pin to tags (or GitHub Releases) rather than a raw commit SHA. **Moderate payoff.**
    - `FEATURE_exe-integration-contract.md` — write an explicit integration contract for `T4CodeGen.exe` in `T4CodeGen/README.md`: response-file grammar, arg semantics, exit codes, stdout/stderr channels, and the `T4Gen_*` output markers (pinned to the actual `Program.cs`/`TemplateCompiler.cs` behavior). **Moderate payoff.**
    - `FEATURE_downstream-consumers-doc.md` — add `docs/DOWNSTREAM-CONSUMERS.md` (exact per-product file manifest for consuming the exe vs the MSBuild task) and `THIRD-PARTY-NOTICES.md` (aggregate the `tools/` NuGet attributions). **Lower payoff / housekeeping.**
- `templates/` — document templates adapted from the WasmTestBedMK1 project: `GoalsTemplate.md` (tier-3 goals), `MilestonesTemplate.md` (tier-2 milestone blocks), `ImplementationPlanTemplate.md` (tier-4 implementation plans). Copy to the relevant plan location and fill in placeholders.

## Work Guidance

## Verification

## Child DOX Index

None.