# plans

## Purpose

Planning and design documents for this repository. Holds the high-level design (`design.md`) and the implementation plans that break it into buildable work.

## Ownership

- Owned by this folder.
- Read by agents planning or implementing changes to the codebase.

## Local Contracts

- `design.md` — project overview, core technology, standalone intent, and pipeline role. Authoritative description of what the project is for.
- `milestones.md` — authoritative milestone definitions (roadmap) at the same tier as the design; each milestone links to its goals file (`goalsN.md`).
- `goalsN.md` — tier-3 goals files, one per milestone, containing per-goal deliverables and acceptance criteria. `goals1.md` = Milestone 1 (Standalone Mono.TextTemplating Engine).
- `implementation plans/` — concrete, sequenced implementation plans (tier-4), derived from the design/goals; only implementation plans live in this subfolder. Current set (Goal 1.1, one plan per deliverable, see `goals1.md`):
  - `GOAL_1_1_host-engine-in-process.md` — Deliverable 1: swap the `t4.exe`/`powershell.exe` shell-out for an in-process Mono.TextTemplating engine (+ Roslyn), keeping parameters, markers, and copy semantics. **Landed 2026-08-30** (vendored under `tools\`, folded Deliverables 2-3).
  - `GOAL_1_1_rename-change-file-mainfest-parameter.md` — Deliverable 2: rename `ChangeFileMainfest` -> `ChangeFileManifest` in task and templates. **Superseded** — folded into the host-engine plan (landed).
  - `GOAL_1_1_preserve-debugging.md` — Deliverable 3: keep `debug="true"` template debugging working after the swap.
  - `GOAL_1_1_failure-semantics.md` — Deliverable 4: per-template failure = clean partial outputs + log error + continue + return `false`.
- `templates/` — document templates adapted from the WasmTestBedMK1 project: `GoalsTemplate.md` (tier-3 goals), `MilestonesTemplate.md` (tier-2 milestone blocks), `ImplementationPlanTemplate.md` (tier-4 implementation plans). Copy to the relevant plan location and fill in placeholders.

## Work Guidance

## Verification

## Child DOX Index

None.