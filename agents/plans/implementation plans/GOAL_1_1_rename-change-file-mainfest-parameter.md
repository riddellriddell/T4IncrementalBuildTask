# GOAL 1.1 - Rename ChangeFileMainfest Parameter to ChangeFileManifest

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `GOAL`
- Task Name: `Rename the misspelled ChangeFileMainfest parameter to ChangeFileManifest`
- Status: `Draft`
- Owner: `"Your Name"`
- Last Updated: `2026-08-29`

## Linked Context

- Design: [design.md](../../design.md)
- Milestone: [milestones.md](../milestones.md)
- Goal: [goals1.md](../goals1.md) (Goal 1.1, Deliverable 2)
- Handover: `<none>`

## Objective

Rename the misspelled template parameter `ChangeFileMainfest` to `ChangeFileManifest` everywhere it appears — the task's parameter passing and every `.tt` declaration/usage — preserving the other two parameter names (`OutputFolder`, `GlobalFileManifest`) and their values exactly, with no stale references to the old spelling left in code or docs.

## Problem Summary

The typo is baked into the task's command line (`BuildT4TextFiles.cs:351`, `-p=ChangeFileMainfest=...`) and into the templates' `<#@ parameter #>` declarations (`HeaderExample.tt:4`, `TestTemplate.tt:4`). Goal 1.1 requires the parameter renamed and fixed in all templates so the task/template parameter names match under the in-process engine.

## Scope

- In scope: `BuildT4TextFiles.cs` parameter-passing name.
- In scope: `HeaderExample.tt` and `TestTemplate.tt` — the `<#@ parameter #>` declarations, the generated property usages (`GetFileLinesAsList(ChangeFileMainfest)` at `HeaderExample.tt:14`, `TestTemplate.tt:23,25`).
- In scope: contract docs that spell the parameter (`CustomBuildTasks/AGENTS.md:16,19`, `T4IntegrationTestBed/T4Templates/AGENTS.md:16-17`).
- Out of scope: the other two parameter names and their values.
- Out of scope: the engine swap itself (`GOAL_1_1_host-engine-in-process.md`) and any behavior change.
- Out of scope: `CodeGenUtilities.ttinclude` — it references no parameter name (helpers only); verified by grep.

## Current State

- Task: `BuildT4TextFiles.cs:351` builds `-p=ChangeFileMainfest='<templateChangedManifestPath>'`.
- Templates: `HeaderExample.tt:4` and `TestTemplate.tt:4` declare `<#@ parameter type="System.String" name="ChangeFileMainfest" #>` and consume it via `GetFileLinesAsList(ChangeFileMainfest)`.
- Docs: `CustomBuildTasks/AGENTS.md:19` and `T4IntegrationTestBed/T4Templates/AGENTS.md:16-17` document the name (including the "(sic ...)" typo note); `agents/plans/goals1.md` and `milestones.md:33` mention the typo in the context of the fix.
- Generated files embed the manifest *value* (paths) and file lists, not the parameter *name*, so this rename produces no content change in the checked-in `*.t4generated.*` files (only a timestemp-driven regeneration when the `.tt` dirtiness triggers it).

## Assumptions and Constraints

- Task and templates must be renamed together (single change set) so the task/template names stay in sync at every build.
- Parameter values remain exactly as today: `OutputFolder` = temp `GeneratedFiles` folder, `GlobalFileManifest` = `GlobalFileManifest.T4Manifest` path, `ChangeFileManifest` = per-template `<TemplateName>.T4ChangedManifest` path.
- No behavior change; this is a rename only.
- Sequencing: this is the recommended first plan of Goal 1.1 so `GOAL_1_1_host-engine-in-process.md` can be written against the corrected name.

## Files and Areas Likely Affected

- `CustomBuildTasks/BuildT4TextFiles.cs` - line 351 `-p=ChangeFileMainfest=` -> `-p=ChangeFileManifest=`. (If the engine swap lands first, apply the rename to the new parameter-passing code instead.)
- `T4IntegrationTestBed/T4Templates/HeaderExample.tt` - line 4 declaration; line 14 usage.
- `T4IntegrationTestBed/T4Templates/TestTemplate.tt` - line 4 declaration; lines 23 and 25 usage.
- `CustomBuildTasks/AGENTS.md` - update the task contract wording (line 19).
- `T4IntegrationTestBed/T4Templates/AGENTS.md` - update the parameter convention (lines 16-17); drop the "(sic ...)" note.

## Implementation Steps

1. **Task rename.** In `BuildT4TextFiles.cs:351`, change `-p=ChangeFileMainfest=` to `-p=ChangeFileManifest=` (or, post engine-swap, the equivalent parameter name passed to the engine).
2. **Template declarations.** In `HeaderExample.tt:4` and `TestTemplate.tt:4`, rename the `<#@ parameter type="System.String" name="ChangeFileMainfest" #>` name to `ChangeFileManifest`.
3. **Template usages.** Rename every `GetFileLinesAsList(ChangeFileMainfest)` call: `HeaderExample.tt:14`; `TestTemplate.tt:23` and `:25`. (This is required because the parameter directive emits a generated property of that name.)
4. **Grep sweep.** `rg -n "ChangeFileMainfest"` across the repo — expect zero matches after steps 1-3 (covers `.tt`, `.ttinclude`, `.cs`, `.targets`, `.md`).
5. **Docs.** Update `CustomBuildTasks/AGENTS.md:19` and `T4IntegrationTestBed/T4Templates/AGENTS.md:16-17` to use `ChangeFileManifest` and drop the "(sic ...)" note. Leave the historical mention in `goals1.md`/`milestones.md` (they describe the fix; optional once milestone completes).
6. **Regenerate.** The `.tt` write makes both templates dirty, so the next build regenerates all outputs (checked-in `*.t4generated.*`). Confirm the regenerated files compile and the task recognizes the corrected parameter.

## Verification Plan

### Automated Checks

- `rg -in "changefilemainfest"` across the repo -> 0 matches.
- `msbuild CustomBuildTasks.csproj` -> compiles without errors.
- `msbuild T4IntegrationTestBed.sln` -> `GenerateT4Files` runs (templates dirty), all `*.t4generated.*` regenerate, app builds and runs.

### Manual Checks

1. Confirm the per-template `.T4ChangedManifest` files still get written and consumed: edit a seed source, rebuild, and verify the corresponding generated files regenerate.
2. Inspect `TestTemplate.t4generated.txt` — the "Changed Files" list still resolves from the dirty-set manifest (parameter value flow intact).

## Risks and Open Questions

- Risk: renaming task and templates in separate commits would break the build in between; rename them in one change set.
- Question: keep the historical note in `goals1.md:45`/`milestones.md:33` after completion? (Default: leave them — they describe the milestone fix as written.)
- Dependency: none beyond the two files plus docs; this plan can ship before the engine swap.

## Completion Checklist

- [ ] Implementation matches the linked design and goal context
- [ ] Scope stayed within this plan
- [ ] Verification steps were completed or explicitly deferred
- [ ] Relevant status docs were updated
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Rename task + templates + docs in a single change; do not touch `OutputFolder`/`GlobalFileManifest`.
- There are no references to the parameter name in `CodeGenUtilities.ttinclude` (grep-verified); leave it unchanged.
- The generated content does not embed the parameter name, so the checked-in outputs only change because their source `.tt` timestamp dirtied them — no manual edits to `*.t4generated.*` files.