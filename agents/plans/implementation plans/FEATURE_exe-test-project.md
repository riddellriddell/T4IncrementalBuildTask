# Implementation Plan: Automated Test Project for the Standalone `T4CodeGen.exe` Front-End

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `FEATURE`
- Task Name: Automated test harness for `T4CodeGen.exe`
- Status: `Landed` (2026-09-05)
- Owner: "Your Name"
- Last Updated: `2026-09-05`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [buildguild.md](../../buildguild.md)
- Milestone: [milestones.md](../milestones.md) (Milestone 2 completed; this is the follow-on hardening work)
- Goal: [goals2.md](../goals2.md) (Goal 2.2 acceptance was manual-only)

## Objective

Ship an automated, offline, zero-NuGet test project (`T4CodeGenTests/`) that exercises `T4CodeGen.exe` as a black box and verifies its shipped contract: exit codes (`0`/`1`/`2`), stdout log / stderr failure forwarding, response files, `|`/`;` list separators, byte-identical incremental regeneration (including the no-op skip), and per-template failure isolation — runnable with plain `msbuild` + a single exe, with no network or test-framework restore.

## Problem Summary

Goal 2.2 (`T4CodeGen.exe`) is verified only by hand: `agents/buildguild.md` describes manual byte-hash diffs and manual exit-code checks against the test bed, and notes a root `test.bat` as "planned but not present yet". There is no repeatable test project, so regressions in the exe's argument parsing, exit-code mapping, stderr forwarding, or incremental behavior go undetected between changes. The natural test frameworks (NUnit/MSTest/xUnit) would require NuGet restore, which the repo's standalone build forbids, so the harness must be a self-contained console runner.

## Scope

- In scope: new `T4CodeGenTests/` console project (.NET Framework 4.7.2, AnyCPU, `System` references only) added to `T4IntegrationTestBed.sln`.
- In scope: fixture set under `T4CodeGenTests/Fixtures/` mirroring the test bed (seed `FancyWrite.h`/`FancyWrite.cpp`/`Main.cpp`, working `TestTemplate.tt` + `HeaderExample.tt` + `CodeGenUtilities.ttinclude`, and a deliberately-broken `Broken.tt`).
- In scope: `Program.cs` harness — `System.Diagnostics.Process` spawns the built `T4CodeGen.exe` against an isolated scratch workspace, runs a case battery, prints `PASS`/`FAIL` per case, and exits `0` only if all pass.
- In scope: root `test.bat` runner (library → exe → tests build, then harness run), per the `buildguild.md` Future note.
- In scope: DOX updates (`T4CodeGenTests/AGENTS.md`, root `AGENTS.md` index, `agents/plans/AGENTS.md` index, `agents/buildguild.md`, `T4CodeGen/AGENTS.md`).
- Out of scope: any change to `T4CodeGen/Program.cs` or `TemplateCompiler.cs` behavior — if the harness exposes a real bug, file a separate `BUG_` plan.
- Out of scope: NuGet/MSTest/xUnit/NUnit test projects, `vstest`, or CI integration — the harness is console-run and msbuild-driven only.
- Out of scope: unit-testing `TemplateCompiler` internals in-process (that is a separate plan; this black-boxes the exe).

## Current State

- `T4CodeGen/Program.cs` is a thin mapper: parses the six task inputs from args or `@response.rsp`, `|`/`;`-splits lists (`AppendList`), calls `TemplateCompiler.Compile`, prints log lines to stdout, forwards `TemplateFailures` to stderr, returns `0`/`1`/`2`.
- Contract (from `T4CodeGen/README.md`): run from the project dir with relative inputs so `T4Gen_TemplateFile`/`T4Gen_InputFile`/`T4Gen_Destination` markers stay identical to the task's; `0` success, `1` any template failed, `2` bad CLI.
- Verified manually only (buildguild flow); the MSBuild task path is verified end-to-end by building the test bed, which is why the exe harness must reproduce the test bed's setup under scratch copy.
- No test/NuGet infrastructure exists in the repo; the vendored `tools\` assemblies are the only dependency copies and the standalone build is offline.

## Assumptions and Constraints

- Exe-under-test path: `T4CodeGen\bin\Debug\T4CodeGen.exe` (build the library and the exe before running tests; Debug matches the hardcoded library path in `RunCodeGen.targets`).
- The harness legitimately uses `System.Diagnostics.Process` to run the exe-under-test. This is the test harness, distinct from the no-shell-out contract on `TemplateCompiler.cs` (`AddMatchingFilesToOutput.cs` is the only file flagged by that grep sweep).
- The spawned exe's `WorkingDirectory` must be the fixture scratch dir (a CWD-relative-input copy of the test bed layout), reproducing the task's relative inputs verbatim.
- Deterministic timestamps are the harness's responsibility: `File.GetLastWriteTime` second-resolution and the `>`/`>=` dirty comparisons mean scratch file mtimes must be explicitly set (e.g. aged to `now − 2 days`, dirty files to `now`) with ≥2s separation between "baseline manifest write" and the next run.
- Scratch dirs are disposable; the harness must never point `BaseIntermediateOutputPath`/`DefaultFileOutputPath` at the real test bed or the checked-in `*.t4generated.*` files.
- No NuGet/network; assert a framework-free harness (compile-time reference only `System`, `System.Core`, `System.Diagnostics` through `System`).
- Every spawned process must have a `WaitForExit` timeout so a hung template fails fast.

## Files and Areas Likely Affected

- `T4CodeGenTests/T4CodeGenTests.csproj` — new classic console project, v4.7.2, AnyCPU, `OutputType=Exe`, `System` references only.
- `T4CodeGenTests/Program.cs` — the case battery and harness (`RunExe`, `ScratchWorkspace` Create/Populate/Cleanup, per-case assert methods, PASS/FAIL report, exit code).
- `T4CodeGenTests/AGENTS.md` — new child DOX doc for the fixture conventions and harness contract.
- `T4CodeGenTests/Fixtures/*` — seed sources, working templates (copied/adapted from `T4Templates/`), `CodeGenUtilities.ttinclude`, and `Broken.tt` (a deliberate compile error, not a runtime exception, for determinism).
- `T4IntegrationTestBed.sln` — add the `T4CodeGenTests` project (classic C# project type GUID `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`, Debug|AnyCPU + Release|AnyCPU mappings).
- `test.bat` (root) — one-command runner: build library + exe + tests offline, then execute the harness exe.
- Docs: `agents/buildguild.md` (Verification Bar + Future note), `agents/plans/AGENTS.md` (index line), root `AGENTS.md` (Child DOX Index entry), `T4CodeGen/AGENTS.md` (Verification: point to the harness).

## Implementation Steps

1. Create `T4CodeGenTests/` project scaffolding: `T4CodeGenTests.csproj` (mirror `T4CodeGen.csproj` structure minus the HintPath/engine references — this project needs none) and `Properties/AssemblyInfo.cs`.
2. Add the project to `T4IntegrationTestBed.sln` with Debug/Release × AnyCPU config mappings and the identical C# project-type GUID used by `T4CodeGen`.
3. Create the fixture set under `T4CodeGenTests/Fixtures/`:
   - `FancyWrite.h` (with `T4Gen_RUN_TEXT_TEMPLATE_ON_THIS(TestAttribute)` tag), `FancyWrite.cpp`, `Main.cpp` — same content names as the test bed so generated filenames match for byte-parity checks.
   - `TestTemplate.tt`, `HeaderExample.tt`, `CodeGenUtilities.ttinclude` — copied from `T4IntegrationTestBed/T4Templates/` (all three task-fed parameters declared, `langversion="latest"`, markers embedded).
   - `Broken.tt` — same preamble parameters but a guaranteed compile error inside the code block (e.g. `int x = "not an int";`) so `ProcessTemplateInProcess` returns `didSucceed=false` deterministically.
   - Normalize fixture file mtimes at copy time (age back so the baseline run is clean).
4. Implement `Program.cs` harness:
   - `RunExe(string workDir, string[] args)` → `(exit, stdout, stderr)` via `ProcessStartInfo` with `WorkingDirectory`, redirected output, ≤60s `WaitForExit`.
   - `ScratchWorkspace.Populate()` copies fixtures into a fresh temp dir, `Cleanup()` removes it after each case.
   - Case battery (each a method with an assert + message):
     - `FreshRegeneration` — first run exits `0`; expected `*.t4generated.*` exist and match expected content.
     - `NoOpRerun` — second run exits `0`; generated bytes unchanged on disk (proves the byte-identical copy skip + clean incremental state).
     - `DirtyInputRegenerates` — set one seed's mtime to now (≥2s after baseline), rerun, confirm the affected generated files actually refreshed and unaffected ones did not.
     - `BrokenTemplateIsolation` — add `Broken.tt` to the input lists; run exits `1`; stderr names `Broken.tt`; the good templates' outputs are still produced; no partial leftovers in the scratch `GeneratedFiles` temp folder.
     - `BadCommandLine` — missing `-Name` and an unknown flag each exit `2` with usage on stderr.
     - `ListSeparators` — semicolon-joined lists produce byte-identical generated output to pipe-joined lists.
     - `ResponseFile` — `@test.rsp` input produces the same exit code and generated bytes as explicit args.
     - `HelpExitZero` — `-h` exits `0` and prints usage to stdout.
   - Report `PASS name`/`FAIL name (reason)` per case; final exit `0` iff all pass.
5. Add root `test.bat`: `msbuild CustomBuildTasks.csproj` → `msbuild T4CodeGen\T4CodeGen.csproj` → `msbuild T4CodeGenTests\T4CodeGenTests.csproj` → run `T4CodeGenTests\bin\Debug\T4CodeGenTests.exe`.
6. DOX pass: create `T4CodeGenTests/AGENTS.md`; add index entries to root `AGENTS.md` and `agents/plans/AGENTS.md`; update `agents/buildguild.md` (replace the "test.bat planned" Future note with the landed harness + new command in the Verification Bar) and `T4CodeGen/AGENTS.md` Verification.

## Verification Plan

### Automated Checks

- `test.bat` (or its three `msbuild` steps + harness run) — offline, no network; must end with the harness printing all cases `PASS` and exiting `0`.
- `msbuild T4IntegrationTestBed.sln` — still builds with the new test project present; task path unregressed.
- Confirm the harness fails loudly when the exe is broken: temporarily corrupt `T4CodeGen/Program.cs` (e.g. always return `2`), rebuild the exe, rerun → nonzero harness exit with a named `FAIL`.

### Manual Checks

1. Run the harness against the checked-in `*.t4generated.*` baseline: reproduce `buildguild.md`'s byte-identity check in `FreshRegeneration` by hash-comparing the scratch outputs against the task-produced set (the fixture mirrors the test bed's file names).
2. Run the existing buildguild flow unchanged to prove the manual path still works.

## Risks and Open Questions

- Risk: clock-granularity flakiness — second-resolution mtimes plus the `>`/`>=` dirty comparisons can produce equal timestamps on fast reruns. Mitigation: harness sets explicit mtimes (`now − 2 days` baseline, `now` for dirty files) and keeps ≥2s between runs that must observe a change.
- Risk: Debug-vs-Release exe path — the harness resolves `T4CodeGen\bin\Debug\T4CodeGen.exe` relative to its own `bin\`. Mitigation: document that the exe must be built (matching the Debug library hardcoded by `RunCodeGen.targets`) before running tests.
- Risk: fixture drift from `T4Templates/` — if templates change, the fixtures go stale. Mitigation: `T4CodeGenTests/AGENTS.md` contract states fixtures mirror `T4Templates/` deliberately and must be refreshed in the same change.
- Question: should the harness later grow a vstest-compatible report? Answer (now): no — offline constraints; a console PASS/FAIL report is the contract.
- Dependency: byte-parity against the task baseline only holds when fixtures and CWD-relative inputs replicate the test bed's; keep file names identical.

## Completion Checklist

- [x] Implementation matches the linked design and goal context
- [x] Scope stayed within this plan
- [x] Verification steps were completed or explicitly deferred
- [x] Relevant status docs were updated
- [x] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Read `agents/buildguild.md` first; the canonical flow is library → exe → test bed. The harness replaces none of it — it automates the exe's part.
- Keep the fixture templates byte-identical to `T4Templates/` where possible so the parity check is honest; only add `Broken.tt` as new fixture.
- Reuse the exact relative inputs from `T4CodeGen/README.md`'s worked example (`FancyWrite.h|FancyWrite.cpp|Main.cpp`, `T4Templates\HeaderExample.tt|T4Templates\TestTemplate.tt`, …) against the scratch CWD when building arg lists.
- `Broken.tt` must be a compile-time error, not a thrown exception, so the failure path is the engine's `didSucceed=false` (compile error) rather than `ProcessTemplateInProcess`'s catch.
- Do not add any `Microsoft.Build.*` or engine references to `T4CodeGenTests.csproj`; the harness talks to the exe only over the process boundary.