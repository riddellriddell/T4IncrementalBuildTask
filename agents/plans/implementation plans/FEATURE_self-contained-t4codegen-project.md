# Implementation Plan: Make `T4CodeGen/` a Self-Contained Unit

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `FEATURE`
- Task Name: Self-contained `T4CodeGen` project (no `ProjectReference` to `CustomBuildTasks`)
- Status: `Landed 2026-09-07`
- Owner: "Your Name"
- Last Updated: `2026-09-07`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [buildguild.md](../../buildguild.md)
- Goal: [goals2.md](../goals2.md) (Goal 2.1 extracted the standalone API; this plan completes the decoupling)
- Handover: N/A

## Objective

Make `T4CodeGen/` a fully self-contained project folder: one `.csproj`, one folder, no `ProjectReference` to `CustomBuildTasks`. Move `TemplateCompiler.cs` and `FileScanUtility.cs` into `T4CodeGen/` so the exe owns the standalone compiler source, and rewire `CustomBuildTasks.csproj` to compile those same files via `<Compile Include="..\T4CodeGen\..." Link="..."/>` (linked-source sharing). Consumers get the exe as a single drop-in folder with no downstream trimming or dependency management.

## Problem Summary

Currently `T4CodeGen.csproj` project-references `CustomBuildTasks.csproj` to bring in `TemplateCompiler`/`TemplateCompilerResult`. This means anyone who wants to use the standalone CLI must either project-reference the full build-task library (pulling in `Microsoft.Build.Framework`, `Microsoft.Build.Utilities`, `BuildT4TextFiles.cs`, `AddMatchingFilesToOutput.cs`, and `Debug.testproj`) or hand-copy the exe and manually trim the csproj — a chore documented as a pain point since Goal 2.2. The two files being shared (`TemplateCompiler.cs` and `FileScanUtility.cs`) are already fully MSBuild-independent (the whole point of Goal 2.1: no `ITaskItem`, no `TaskLoggingHelper`, no `Microsoft.Build.*` using-directives). The coupling is purely structural, not technical, and should be dissolved.

## Scope

- In scope: move `TemplateCompiler.cs` and `FileScanUtility.cs` from `CustomBuildTasks/` into `T4CodeGen/`.
- In scope: rewire `T4CodeGen.csproj` to compile the moved `.cs` files directly (no `ProjectReference` to `CustomBuildTasks`).
- In scope: rewire `CustomBuildTasks.csproj` to compile the moved `.cs` files via `<Compile Include="..\T4CodeGen\TemplateCompiler.cs" Link="TemplateCompiler.cs"/>` (linked-source), so the MSBuild task DLL still contains `TemplateCompiler` and `FileScanUtility` without duplication.
- In scope: verify both projects build, the MSBuild task DLL works end-to-end against the test bed, the CLI exe works, and the `T4CodeGenTests` black-box battery passes.
- In scope: DOX updates (`T4CodeGen/AGENTS.md`, `CustomBuildTasks/AGENTS.md`, root `AGENTS.md`, `agents/plans/AGENTS.md` index).
- Out of scope: moving `tools/` vendored assemblies into `T4CodeGen/` (that is a separate plan — the HintPath references stay as-is).
- Out of scope: changing `RunCodeGen.targets` or the `CustomBuildTasks.dll` output path.
- Out of scope: changing the namespace (`T4BuildTools`) of either file, or any code change to `TemplateCompiler.cs` / `FileScanUtility.cs`.
- Out of scope: changing the `TemplateCompiler.Compile` API surface, the incremental algorithm, or per-template failure semantics.

## Current State

**Files to move:**

- `CustomBuildTasks/TemplateCompiler.cs` — namespace `T4BuildTools`, 535 lines. Contains `TemplateCompilerResult` (result DTO: `Success` + `TemplateFailures`) and `TemplateCompiler` (static `Compile` method + private `ProcessTemplateInProcess` helper). Uses only `System.*` and `Mono.TextTemplating`. Internally calls `FileScanUtility.*` for regex scanning and file-list operations. No `Microsoft.Build.*` references.
- `CustomBuildTasks/FileScanUtility.cs` — namespace `T4BuildTools`, 141 lines. Static helpers: `ScanFileWithRegex`, `ConvertMatchListToStringList`, `ConvertFileListToExistingFileList`, `ConvertFileListToChangedSinceFileList`. Uses only `System.*`. No `Microsoft.Build.*` references.

**Consumers of these two files:**

- `CustomBuildTasks/BuildT4TextFiles.cs` — references `TemplateCompiler` and `TemplateCompilerResult` by name (same namespace `T4BuildTools`). This file stays in `CustomBuildTasks/`.
- `T4CodeGen/Program.cs` — references `TemplateCompiler` and `TemplateCompilerResult` by name (imports `using T4BuildTools`). This file stays in `T4CodeGen/`.
- `CustomBuildTasks/AddMatchingFilesToOutput.cs` — does NOT use either file; uses its own `ProcessStartInfo`/`cmd.exe` approach. Stays in `CustomBuildTasks/`, unaffected.
- `T4CodeGenTests/` — black-boxes `T4CodeGen.exe` over the process boundary; never references `TemplateCompiler` or `FileScanUtility` directly. Unaffected.

**Current csproj wiring:**

- `T4CodeGen.csproj` (line 50): `<ProjectReference Include="..\CustomBuildTasks\CustomBuildTasks.csproj">` — must be removed.
- `CustomBuildTasks.csproj` (lines 49–50): `<Compile Include="FileScanUtility.cs" />` and `<Compile Include="TemplateCompiler.cs" />` — must be replaced with linked-source references pointing into `T4CodeGen/`.

**RunCodeGen.targets (line 12):** `<UsingTask ... AssemblyFile="$(MSBuildThisFileDirectory)CustomBuildTasks\bin\Debug\CustomBuildTasks.dll" />` — must remain intact; the library output path does not move.

## Assumptions and Constraints

- `TemplateCompiler.cs` and `FileScanUtility.cs` must remain a **single source of truth** — one copy of each file, compiled by both projects. No duplication.
- The namespace stays `T4BuildTools` to avoid churn in all consuming files (`BuildT4TextFiles.cs`, `Program.cs`, templates that reference the engine, and any downstream code).
- The standalone build must remain offline: no NuGet restore, no network. `tools/` HintPath references are unchanged.
- `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` is the exact path hardcoded in `RunCodeGen.targets`; this output path must not move.
- The linked-source approach (`<Compile Include="..\T4CodeGen\..." Link="..."/>`) compiles the file into both assemblies from a single disk location — no duplication, no drift risk.
- `T4CodeGen.csproj` must NOT reference `Microsoft.Build.Framework` or `Microsoft.Build.Utilities` after the change (currently it doesn't, and the moved files don't either — this constraint is automatically satisfied).
- `TemplateCompiler.cs` references `Mono.TextTemplating` and `Mono.TextTemplating.Roslyn` at compile time; both csprojs already carry HintPath references to `tools/` for these assemblies, so no new references are needed.

## Files and Areas Likely Affected

- `CustomBuildTasks/TemplateCompiler.cs` — moved to `T4CodeGen/TemplateCompiler.cs` (git mv).
- `CustomBuildTasks/FileScanUtility.cs` — moved to `T4CodeGen/FileScanUtility.cs` (git mv).
- `T4CodeGen/T4CodeGen.csproj` — remove the `ProjectReference` to `CustomBuildTasks.csproj`; add `<Compile Include="TemplateCompiler.cs" />` and `<Compile Include="FileScanUtility.cs" />` (the files are now local).
- `CustomBuildTasks/CustomBuildTasks.csproj` — replace the direct `<Compile Include="FileScanUtility.cs" />` and `<Compile Include="TemplateCompiler.cs" />` items with `<Compile Include="..\T4CodeGen\TemplateCompiler.cs" Link="TemplateCompiler.cs" />` and `<Compile Include="..\T4CodeGen\FileScanUtility.cs" Link="FileScanUtility.cs" />` (linked-source).
- `T4CodeGen/AGENTS.md` — update Local Contracts to reflect that `T4CodeGen.csproj` no longer Project-references `CustomBuildTasks` and now owns the two `.cs` files directly.
- `CustomBuildTasks/AGENTS.md` — update Local Contracts to reflect that `TemplateCompiler.cs` and `FileScanUtility.cs` live in `T4CodeGen/` and are consumed via linked-source.
- `agents/plans/AGENTS.md` — add index entry for this plan; update any wording about the exe's dependency on `CustomBuildTasks.dll`.
- Root `AGENTS.md` — update Child DOX Index entry for `T4CodeGen/` to reflect self-contained status.
- `agents/plans/design.md` — no changes needed (the standalone intent description is already accurate).

## Implementation Steps

1. **Move the two source files** via `git mv`:
   - `git mv CustomBuildTasks/TemplateCompiler.cs T4CodeGen/TemplateCompiler.cs`
   - `git mv CustomBuildTasks/FileScanUtility.cs T4CodeGen/FileScanUtility.cs`

2. **Update `T4CodeGen/T4CodeGen.csproj`** — make the exe self-contained:
   - Remove the entire `<ItemGroup>` containing the `<ProjectReference>` to `CustomBuildTasks.csproj` (lines 48–54).
   - Add a new `<ItemGroup>` with direct `<Compile>` items for the moved files:
     ```xml
     <ItemGroup>
       <Compile Include="TemplateCompiler.cs" />
       <Compile Include="FileScanUtility.cs" />
     </ItemGroup>
     ```
   - Keep all existing `<Reference>` HintPath entries for `Mono.TextTemplating`, `Microsoft.CodeAnalysis*`, and `System.*` runtime assemblies unchanged.

3. **Update `CustomBuildTasks/CustomBuildTasks.csproj`** — consume moved files via linked-source:
   - Replace `<Compile Include="FileScanUtility.cs" />` with `<Compile Include="..\T4CodeGen\FileScanUtility.cs" Link="FileScanUtility.cs" />`.
   - Replace `<Compile Include="TemplateCompiler.cs" />` with `<Compile Include="..\T4CodeGen\TemplateCompiler.cs" Link="TemplateCompiler.cs" />`.
   - Keep all other `<Compile>` items (`BuildT4TextFiles.cs`, `AddMatchingFilesToOutput.cs`, `Properties\AssemblyInfo.cs`) and all `<Reference>` HintPath entries unchanged.

4. **Verify both projects build independently (offline, no network):**
   - `msbuild CustomBuildTasks\CustomBuildTasks.csproj` — must produce `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` with `TemplateCompiler` and `FileScanUtility` types present.
   - `msbuild T4CodeGen\T4CodeGen.csproj` — must produce `T4CodeGen\bin\Debug\T4CodeGen.exe` with no reference to `CustomBuildTasks.dll` (check with ILSpy or `dumpbin /dependents` — only `System.*` and `tools/` assemblies should appear).

5. **Verify the MSBuild task end-to-end** — build the test bed and confirm `GenerateT4Files` regenerates `*.t4generated.*` files in-process (the task DLL contains `TemplateCompiler` via linked-source).

6. **Run the black-box test battery** — `test.bat` or `msbuild T4CodeGenTests\T4CodeGenTests.csproj` then `T4CodeGenTests\bin\Debug\T4CodeGenTests.exe`. All cases must print `PASS` and exit `0`.

7. **Verify exe independence** — confirm `T4CodeGen\bin\Debug\` contains `T4CodeGen.exe` and the `tools/` runtime DLLs but NOT `CustomBuildTasks.dll`.

8. **DOX pass:**
   - Update `T4CodeGen/AGENTS.md`: Local Contracts must reflect that the project now owns `TemplateCompiler.cs` and `FileScanUtility.cs` directly and has no `ProjectReference` to `CustomBuildTasks`.
   - Update `CustomBuildTasks/AGENTS.md`: Local Contracts must reflect that `TemplateCompiler.cs` and `FileScanUtility.cs` are consumed via linked-source from `T4CodeGen/`.
   - Update root `AGENTS.md`: Child DOX Index entry for `T4CodeGen/` should mention it is self-contained.
   - Update `agents/plans/AGENTS.md`: add index entry for this plan and mark its status.

## Verification Plan

### Automated Checks

- `msbuild CustomBuildTasks\CustomBuildTasks.csproj` — must succeed; `bin\Debug\CustomBuildTasks.dll` must contain types `T4BuildTools.TemplateCompiler`, `T4BuildTools.TemplateCompilerResult`, and `T4BuildTools.FileScanUtility` (verify via ILSpy, `ildasm`, or `type` reflection check).
- `msbuild T4CodeGen\T4CodeGen.csproj` — must succeed; `bin\Debug\T4CodeGen.exe` must NOT reference `CustomBuildTools.dll` (`dumpbin /dependents` shows only `System.*` and `tools/` assemblies).
- `test.bat` (library → exe → tests → harness run) — all `T4CodeGenTests` cases must print `PASS` and exit `0`.
- `msbuild T4IntegrationTestBed.sln` — full solution build must succeed with no targets changes and the test bed's `*.t4generated.*` files regenerated identically.

### Manual Checks

1. Inspect `T4CodeGen\bin\Debug\` contents: `T4CodeGen.exe` plus `tools/` runtime DLLs only — no `CustomBuildTasks.dll` present.
2. Inspect `CustomBuildTasks\bin\Debug\` contents: `CustomBuildTasks.dll` plus `tools/` runtime DLLs — same as before this change.
3. Run the existing buildguild manual flow (build library, build test bed, confirm generated files) to prove the MSBuild task path is unregressed.

## Risks and Open Questions

- Risk: linked-source compilation order — MSBuild compiles all `<Compile>` items in a project regardless of source location; `Link` metadata is cosmetic only (affects IDE display, not compilation). No risk to correctness, but worth verifying both projects still list all needed files.
- Risk: IDE experience — Visual Studio may show the linked files under a different virtual folder. This is cosmetic only and does not affect builds.
- Question: should the `TemplateCompiler.cs` namespace stay `T4BuildTools` or change to `T4CodeGen`? Recommendation: keep `T4BuildTools` — changing it would require updating `BuildT4TextFiles.cs`, `Program.cs`, and any template code that references the types. The namespace is an internal detail; the folder boundary is the meaningful one.
- Question: will any downstream consumer that project-references `CustomBuildTasks.dll` break because `TemplateCompiler` is now compiled via linked-source instead of being a "native" file? No — the type is in the same assembly with the same fully-qualified name; linked-source is invisible at the binary level.
- Dependency: the `tools/` HintPath references in `T4CodeGen.csproj` already exist and are unchanged; no new vendoring is required.

## Completion Checklist

- [x] Implementation matches the linked design and goal context
- [x] Scope stayed within this plan
- [x] Verification steps were completed or explicitly deferred
- [x] Relevant status docs were updated
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Do a clean build of both projects after the move (delete `bin\` and `obj\` folders) to catch any stale references.
- The `ProjectReference` removal in step 2 is the key win — after that, the exe's `bin\Debug\` output contains zero `CustomBuildTasks` artifacts. Verify this explicitly.
- Do NOT change the `tools/` HintPath references — they are unchanged and already point to the correct vendored assemblies from both project directories.
- `AddMatchingFilesToOutput.cs` does not use `TemplateCompiler` or `FileScanUtility`; it is unaffected by this change.
- `Debug.testproj` is stale and references the old file locations; it is explicitly out of scope — leave it as-is (it was already broken before this change).
- The `Link` metadata on the linked-source `<Compile>` items in `CustomBuildTasks.csproj` ensures the files appear correctly in IDE project views; without it, MSBuild still compiles them but the IDE may show confusing relative paths.
