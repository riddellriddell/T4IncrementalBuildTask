# GOAL 1.1 - Per-Template Failure Semantics

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `GOAL`
- Task Name: `Failure handling: clean up, continue, and fail the build per template`
- Status: `Landed 2026-08-31` (Deliverable 4 — implemented + verified)
- Owner: `"Your Name"`
- Last Updated: `2026-08-31`

## Linked Context

- Design: [design.md](../../design.md)
- Milestone: [milestones.md](../milestones.md)
- Goal: [goals1.md](../goals1.md) (Goal 1.1, Deliverable 4)
- Handover: `<none>`

## Objective

When an individual template fails to compile or execute under the in-process engine, the task must: remove that template's partial outputs from the temporary `GeneratedFiles` folder, log a clear error naming the template, continue processing the remaining `.tt` files, and return `false` from `Execute()` so the build fails after all templates were attempted.

## Problem Summary

The current code never inspects template success: the polling loop reads stdout/stderr but ignores the exit code, and `Execute()` always `return true` (`BuildT4TextFiles.cs:370-393, 492`). After the engine swap the engine surfaces per-template success flags and error text; without handling, a broken template would silently pass the build and leave partial generated files behind. Goal 1.1 requires clean-up + continue + fail semantics.

## Scope

- In scope: per-template success capture in the in-process engine loop (added by `GOAL_1_1_host-engine-in-process.md`).
- In scope: deleting the failing template's partial outputs written into the temp `GeneratedFiles` folder before continuing.
- In scope: `Log.LogError` (MSBuild error) per failed template so the build reports the cause.
- In scope: continue to the next template after a failure; `return false` if any template failed (successful templates may still have their outputs copied).
- In scope: keeping the pre-existing full-folder cleanup of `GeneratedFiles` before generation (lines 300-313) and the invalid-file deletion afterwards (484-490) unchanged.
- Out of scope: changing the incremental-scan algorithm or the marker contract.
- Out of scope: template-side changes — templates are not modified by this plan.

## Current State

- After the dirty-set computation, the task writes each dirty template's `.T4ChangedManifest` and (today) spawns `t4` via `powershell.exe`, then polls `Process.HasExited` and prints stdout/stderr without checking `ExitCode` (lines 370-393).
- `Execute()` returns `true` unconditionally (line 492).
- Post-engine-swap (per Deliverable 1), `templateFile` loops call an in-process helper returning a success flag + error text; this plan builds on that return value.

## Assumptions and Constraints

- The engine host from Deliverable 1 returns per-template success/error (compilation errors, exceptions, warnings) to the task.
- "Partial outputs" means files in `tempGeneratedFilesFolder` (`BaseIntermediateOutputPath\GeneratedFiles\`) created during that template's run — snapshot the folder listing before the run and delete the delta after a failure.
- A failing template must not prevent other templates from running or from having their outputs copied.
- The overall task result becomes `false` only if any template failed; `false` fails the MSBuild target while still having attempted all templates.
- Failure detection must not change on the happy path: no failures -> identical outputs and `return true`.

## Files and Areas Likely Affected

- `CustomBuildTasks/BuildT4TextFiles.cs` - the in-process loop introduced/changed by Deliverable 1: capture success, snapshot/cleanup temp outputs, log errors, track overall result, and set the return value.
- `CustomBuildTasks/AGENTS.md` - document the failure contract (clean up + continue + fail) in the task Local Contracts once implemented.

## Implementation Steps

1. **Capture per-template result.** In the engine loop (from Deliverable 1), keep the success flag and error text the in-process host returns for each dirty template.
2. **Snapshot before each run.** Before invoking the engine for a template, snapshot `Directory.GetFiles(tempGeneratedFilesFolder)`; after the run, this is the "before" set.
3. **Handle failure.** If the template failed:
   - Delete the delta — files in the temp folder that did not exist in the "before" snapshot (partial outputs written mid-run).
   - `Log.LogError($"T4 Template {templateFilePath} failed: {errorText}")` with the template path and engine error text.
   - Record `anyTemplateFailed = true`.
   - `continue` to the next template (do not abort the loop).
4. **Handle success unchanged.** Process successful templates exactly as the engine-swap plan does; the downstream gather/copy logic (lines 395-461) is untouched.
5. **Return value.** Replace `return true` with `return !anyTemplateFailed;` so the build fails after all templates were attempted when any failed.
6. **Update the contract doc.** In `CustomBuildTasks/AGENTS.md` add the local contract: "A failing template removes its partial outputs from the temp GeneratedFiles folder, logs an error, and continues; the task returns false if any template failed."

## Verification Plan

### Automated Checks

- `msbuild T4IntegrationTestBed.sln` with no template errors — succeeds, outputs identical, return value `true`.
- `msbuild CustomBuildTasks.csproj` — compiles clean.

### Manual Checks

1. **Deliberate failure drill:** add a temporarily-throwing template (e.g. a `<# throw new Exception("boom"); #>` body in a scratch template wired into the project, or temporarily edit an existing template to throw).
2. Build and confirm: (a) an MSBuild error is logged naming the failing template; (b) the remaining template still runs and its `*.t4generated.*` files are produced; (c) no partial outputs for the failing template remain in `$(Platform)\$(Configuration)\obj\GeneratedFiles`; (d) the build exits non-zero.
3. Revert the deliberate break and rebuild — green again, no partial outputs, `Execute()` returns `true`.

## Risks and Open Questions

- Risk: an exception thrown inside the engine host may surface as an unhandled exception rather than a success flag; wrap the host call in try/catch and treat exceptions as template failures (log error text + exception message).
- Risk: the snapshot-delta heuristic for "partial outputs" fails if one template legitimately drains files written by a previous template's run (not the case today — each template writes distinct `.t4generated.*` outputs); reassess if templates ever share output filenames.
- Question: should successful templates' outputs still be copied when another template failed? (Recommended: yes — copy successes, return false overall. Confirm with owner if they prefer copying none on any failure.)
- Question: is a per-template error enough, or should the task also summarize "N of M templates failed"? (Default: per-template errors are sufficient.)
- Dependency: Deliverable 1 (`GOAL_1_1_host-engine-in-process.md`) must be complete; the in-process loop must be sequential (as specified there) for snapshot-delta cleanup to be unambiguous.

## Completion Checklist

- [x] Implementation matches the linked design and goal context
- [x] Scope stayed within this plan
- [x] Verification steps were completed or explicitly deferred
- [x] Relevant status docs were updated
- [x] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- This deliverable changes only the post-Deliverable-1 loop and the return value; preserve the happy path byte-for-byte.
- Use `Log.LogError` (MSBuild error) rather than `Console.WriteLine` so the failure is visible in the MSBuild/VS error list.
- Keep the existing "Running T4 Template ... with dirty files" logging for successful runs; add a distinct failure log line.
- The deliberate-failure drill (Manual Checks) is the acceptance evidence for "A template that throws fails only that template, cleans up its partial outputs, continues the others, and causes the overall task to return false."