# GOAL 1.1 - Host Mono.TextTemplating In-Process (Engine Swap)

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `GOAL`
- Task Name: `Host Mono.TextTemplating in-process (remove t4.exe shell-out & PATH)`
- Status: `Landed 2026-08-30` (Deliverable 1, plus folded Deliverables 2-3; Deliverable 4 tracked by `GOAL_1_1_failure-semantics.md`)
- Owner: `"Your Name"`
- Last Updated: `2026-08-30`

## Linked Context

- Design: [design.md](../../design.md)
- Milestone: [milestones.md](../milestones.md)
- Goal: [goals1.md](../goals1.md) (Goal 1.1, Deliverable 1)
- Handover: `<none — first plan in the Goal 1.1 sequence>`

## Objective

Swap the `powershell.exe` + `t4 -p=...` shell-out on `BuildT4TextFiles.cs` lines 316-393 for a direct, in-process `TemplateGenerator`/`TemplatingEngine` call from Mono.TextTemplating (3.0.0) with the in-process Roslyn compiler (`Mono.TextTemplating.Roslyn` -> `UseInProcessCompiler()`), so template transformation depends on no `t4`, no Visual Studio/MSVC install, and no `PATH` entry — while preserving incremental scanning, dependency markers, destination resolution, and copy semantics exactly.

## Problem Summary

The task spawns `powershell.exe` and runs the VS-installed `t4.exe` (`BuildT4TextFiles.cs:350-366`). That couples the component to a Visual Studio/MSVC installation and requires `t4` on `PATH`, which contradicts the "Standalone Intent" in `design.md` and breaks build-pipeline usage in environments without that install. Replacing the shell-out with the bundled engine removes the coupling.

## Scope

- In scope: add `Mono.TextTemplating` and `Mono.TextTemplating.Roslyn` package references to `CustomBuildTasks.csproj` (classic csproj, .NET Framework 4.7.2).
- In scope: replace the process-spawn + polling-loop block (`BuildT4TextFiles.cs` lines 316-393) with sequential in-process engine invocation per dirty template.
- In scope: pass `OutputFolder`, `ChangeFileManifest` (post-rename), and `GlobalFileManifest` through the engine's parameter mechanism so the templates' `<#@ parameter #>` directives keep working.
- In scope: remove `Process`, `powershell.exe`, `t4 -p=`, and the `t4` PATH requirement from the task; update affected docs (root `AGENTS.md` line 6, `design.md`, `CustomBuildTasks/AGENTS.md`).
- Out of scope: the parameter rename (`GOAL_1_1_rename-change-file-mainfest-parameter.md`) — assumed already shipped.
- Out of scope: incremental-scan algorithm (`Execute()` lines 44-269), the comment-marker contract, destination resolution, and byte-identical copy skip — all unchanged.
- Out of scope: per-template failure handling and `return false` semantics (`GOAL_1_1_failure-semantics.md` — a follow-up); this plan preserves current success behavior.
- Out of scope: retargeting off .NET Framework 4.7.2.

## Current State

- `BuildT4TextFiles.Execute()`: after computing per-template dirty sets and writing the per-template `.T4ChangedManifest` (lines 331-347), it builds a `t4 -p=...` command (lines 350-352), spawns `powershell.exe` per dirty template (lines 356-366), and polls `Process` exit (lines 370-393). It never inspects the exit code and always `return true` (line 492).
- `CustomBuildTasks.csproj`: classic non-SDK csproj (`ToolsVersion="15.0"`, `<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>`), MSBuild `<Reference>` items only — no `PackageReference` or `packages.config` today.
- Templates already carry `language="C#" debug="true" hostSpecific="true"` and `<#@ parameter #>` directives; the `<#@ parameter #>` flow is the target of parameter passing.

## Assumptions and Constraints

- Package versions pinned to `3.0.0` per the goal's External Dependencies section.
- `Mono.TextTemplating.Roslyn` 3.0.0 explicitly targets .NET Framework 4.7.2 (per NuGet), so the engine hosts in-process under net472. It bundles Roslyn (Microsoft.CodeAnalysis) so no `csc`/dotnet SDK is needed.
- The task runs in-proc inside MSBuild; the engine must not shell out to any external compiler.
- Keep everything outside lines 316-393 byte-identical: GeneratedFiles cleanup (300-313), gather/copy logic (395-461), invalid-file deletion (484-490).
- The parameter names other than the rename-target stay exactly `OutputFolder`, `GlobalFileManifest`, and (renamed) `ChangeFileManifest`.
- Sequencing constraint: this plan assumes `GOAL_1_1_rename-change-file-mainfest-parameter.md` landed first so the engine call passes the corrected name.
- NuGet restore requires network access and a reachable nuget.org feed on the build machine.

## Files and Areas Likely Affected

- `CustomBuildTasks/CustomBuildTasks.csproj` - add PackageReference(s) for Mono.TextTemplating 3.0.0 + Mono.TextTemplating.Roslyn 3.0.0.
- `CustomBuildTasks/BuildT4TextFiles.cs` - replace lines 316-393 (process spawn + poll loop) with the in-process engine host; drop now-unused usings if `Process` is no longer referenced.
- `CustomBuildTasks/AGENTS.md` - update the task contract (line 19) and the verification note (line 30) to remove `t4`/`powershell`/PATH wording.
- `agents/plans/design.md` - update "T4" and "Standalone Intent" wording (lines 13, 20-25) once the swap lands.
- `AGENTS.md` (root) - line 6 states `t4.exe` must be on PATH; remove that requirement.

## Implementation Steps

1. **Add package references.** In `CustomBuildTasks.csproj` add
   `<PackageReference Include="Mono.TextTemplating" Version="3.0.0" />` and
   `<PackageReference Include="Mono.TextTemplating.Roslyn" Version="3.0.0" />` (classic csproj supports PackageReference under MSBuild/NuGet 4+). Run `msbuild CustomBuildTasks.csproj /t:Restore` and confirm restore resolves. If restore fails under the classic project layout, fall back to `packages.config` + `packages/` + `<Reference HintPath=...>` and record that in this plan.
2. **Build the in-process host.** In `BuildT4TextFiles.cs`, create a private helper (e.g. `bool ProcessTemplateInProcess(string templatePath, string outputFolder, string globalManifestPath, string changedManifestPath, out string errorText)`):
   - Construct the engine with the in-process compiler: `TemplatingEngine engine = new TemplatingEngine(); engine.UseInProcessCompiler();` wrapped by a `TemplateGenerator` (instance or a minimal subclass implementing the host).
   - Feed the three parameters via the generator's parameter mechanism so the `<#@ parameter #>` directives resolve: `AddParameter(null, null, "OutputFolder", value)` (and `GlobalFileManifest`, `ChangeFileManifest`). Verify against the 3.0.0 assembly whether the API is `AddParameter(...)`, `Parameters[name] = value`, or session entry — the `<#@ parameter #>` directive processor reads through the host's `ResolveParameterValue`.
   - Return success + collected error/warning text.
3. **Replace the spawn loop.** Delete lines 316-393. For each `<templateFile, dirtySet>` pair with a non-empty dirty set: log the existing "Running T4 Template ..." line, write the `.T4ChangedManifest` (logic from lines 340-347, unchanged), and call the helper. Keep sequential processing for now (templates were effectively concurrent before; the two test templates have no cross-dependencies, and sequential simplifies failure handling — revisit parallel only if template count grows).
4. **Remove the shell-out artifacts.** Delete the `ProcessStartInfo`, `powershell.exe`, `t4 -p=` string construction, `activeProcesses`, and the polling loop. Remove `using System.Diagnostics;` if `Process` is no longer referenced.
5. **Leave the downstream logic untouched.** The post-generation block (lines 395-461: destination resolution via `T4Gen_Destination`, skip-identical copy) and the invalid-file deletion (484-490) stay byte-identical. `return true` is preserved (failure handling is `GOAL_1_1_failure-semantics.md`).
6. **Docs DOX pass.** Update `CustomBuildTasks/AGENTS.md` (task contract + verification), `agents/plans/design.md` (T4/standalone wording), and root `AGENTS.md` line 6 (drop the `t4.exe` on-PATH requirement) to describe the in-process engine.

## Verification Plan

### Automated Checks

- `msbuild CustomBuildTasks.csproj /t:Restore` then `msbuild CustomBuildTasks.csproj` — build succeeds with no errors; produces `bin\Debug\CustomBuildTasks.dll`.
- Confirm `bin\Debug\` contains `Mono.TextTemplating.dll`, `Mono.TextTemplating.Roslyn.dll` and the Roslyn (`Microsoft.CodeAnalysis*`) assemblies so the task is self-contained for the engine step.
- `rg -i "powershell|t4 -p|ProcessStartInfo" CustomBuildTasks` — no matches in compiled sources.
- Build `T4IntegrationTestBed.sln` with `t4` absent/off PATH — success (see Manual step 1 first).

### Manual Checks

1. Make `t4` unavailable: remove/rename its PATH entry (or `-Command "Remove-Item ..."`) and confirm `Get-Command t4` fails, then build the solution in a fresh shell.
2. Force regeneration: touch a seed (e.g. `T4IntegrationTestBed\FancyWrite.h`), rebuild, and confirm `GenerateT4Files` regenerates the matching `*.t4generated.*` files and the app compiles and runs.
3. Incremental check: rebuild with no changes — templates with empty dirty sets are skipped; `*.t4generated.*` files are not rewritten.
4. Content check: `TestTemplate.t4generated.txt` shows the correct output-folder and global-manifest paths (proves parameter passing survived the swap), and the marker comments (`T4Gen_TemplateFile` / `T4Gen_InputFile` / `T4Gen_Destination`) are intact.

## Risks and Open Questions

- Risk: classic-csproj `PackageReference` restore can misbehave under older NuGet/MSBuild; fallback is `packages.config` + HintPath references.
- Risk: the exact 3.0.0 API differs from the docs (e.g. `AddParameter` vs `Parameters[...]`, `TemplateGenerator` requirement to subclass, sync `ProcessTemplate` vs async). Verify against the restored assembly before writing the host; plan for a minimal subclass if `TemplateGenerator` is abstract.
- Risk: `<#@ parameter #>` + `hostSpecific="true"` resolution path in Mono.TextTemplating may need `Host.ResolveParameterValue`/session tuning beyond `AddParameter`; validate with both templates.
- Question: sequential vs parallel template execution. Kept sequential here; revisit if more templates are added.
- Dependency: NuGet restore needs network + feed access on the machine running the build.
- Dependency: the parameter rename plan must land first (or be folded into step 3).

## Completion Checklist

- [x] Implementation matches the linked design and goal context
- [x] Scope stayed within this plan (documented deviations above: vendoring under `tools/`, `Directory.CreateDirectory` fix, `langversion` attribute, `rg` scoped to `BuildT4TextFiles.cs`)
- [x] Verification steps were completed or explicitly deferred (all Automated + Manual checks passed; see Execution Record)
- [x] Relevant status docs were updated (goals1.md, milestones.md, design.md, buildguild.md, root + child AGENTS.md docs)
- [ ] A handover document was created if the work stopped mid-phase — N/A (completed; no mid-phase stop)

## Notes for the Implementing Agent

- Change only lines 316-393 (plus usings and the csproj/dos); leave the incremental scan, manifest writes, gather/copy, and invalid-file deletion byte-identical.
- Prefer the high-level `TemplateGenerator` API per the goal's "Development Approach" unless finer `TemplateSettings.CompilerOptions` control is needed.
- Debug support (`debug="true"`) is validated by `GOAL_1_1_preserve-debugging.md`; keep the engine honoring the directive (don't strip debug from settings).
- Templates' `Console.WriteLine` output now goes to the task's own process; keep the existing "running/removing/copying" log lines so build diagnostics don't regress.

## Execution Record (landed 2026-08-30)

Implemented and verified end-to-end. Deviations from this plan's letter, and findings:

1. **Vendoring instead of PackageReference (user-mandated).** The user directed that all downloaded files live in the project folder under `tools`, so the assemblies are vendored at `tools\<package>\<version>\` and referenced via `<Reference><HintPath>` — no `<PackageReference>`, no `packages.config`, no restore step. The library builds standalone with zero network access. Keeps the "Standalone Intent" a step further than the plan assumed.
2. **The parameter rename (Deliverable 2) was folded in here**, as the plan's sequencing note permits: `ChangeFileMainfest` -> `ChangeFileManifest` in `BuildT4TextFiles.cs`, `HeaderExample.tt`, `TestTemplate.tt`. The rename plan is superseded (see its metadata).
3. **Language version.** Mono's in-process compiler defaults template code to C# 5 (the classic VS T4 baseline), but the templates use C# 6 string interpolation. Fixed the supported way: `langversion="latest"` added to the `<#@ template #>` directive in both `.tt` files (parsed by `TemplatingEngine.GetSettings` into `TemplateSettings.LangVersion`). `TemplateGenerator` (3.0.0) exposes no `Settings` property, so the template directive is the only override path under the high-level API. No task-code change needed.
4. **API facts verified against the 3.0.0 assemblies:** `Mono.TextTemplating.Roslyn.dll` exports a single type, `Mono.TextTemplating.RoslynTemplatingEngineExtensions` — there is no `Mono.TextTemplating.Roslyn` namespace; `using Mono.TextTemplating;` is sufficient for `UseInProcessCompiler()`. The sync 4-arg `ProcessTemplate(string, string, ref string, out string)` is `[Obsolete]` (third param is `ref`), so the helper calls `generator.ProcessTemplateAsync(templatePath, templateContent, null).GetAwaiter().GetResult()`; the null output file name writes no junk file. `AddParameter(null, null, name, value)` works because `ResolveParameterValue` falls back to the `(null, null, name)` key and both templates are `hostSpecific="true"`.
5. **Pre-existing first-build bug fixed (2 lines, still in scope as "outside 316-393 kept byte-identical" was relaxed for a real defect):** `File.WriteAllText(allFilesManifestPath, ...)` threw `DirectoryNotFoundException` on the first build because `BaseIntermediateOutputPath`'s `obj` folder did not yet exist; added `Directory.CreateDirectory(BaseIntermediateOutputPath);` just before the write. Without it the fresh-build acceptance could not pass.
6. **Solution-build ordering (pre-existing, NOT changed here):** `T4IntegrationTestBed.sln` lists `T4IntegrationTestBed` before `CustomBuildTasks` with no project dependency, so a solution build from a wiped library `bin` fails `MSB4062` (task dll cannot be loaded). The canonical recipe (also in `agents/buildguild.md`) therefore builds the library first, then the test bed. Fixing the sln ordering is out of scope for this plan and is documented as a known gap in `agents/buildguild.md`.
7. **`rg` verification scoped to `BuildT4TextFiles.cs`:** `AddMatchingFilesToOutput.cs` is an unreferenced prototype that still uses `ProcessStartInfo`/`t4 -v`; it stays untouched and is documented as such in `CustomBuildTasks/AGENTS.md`.
8. **Incremental no-op verified:** a same-state rebuild detects no dirty templates and skips regeneration (msbuild vcxproj twice; second run `T4 text generation took: 0 seconds`, no dirty files). Parameter passing verified via `TestTemplate.t4generated.txt` (correct `Output folder:` / `Global Manifest :` values) with `t4` absent from PATH.
9. **Checked-in `*.t4generated.*` deltas on first in-process run:** the stale `TestTemplate.t4generated.text` leftover (from the pre-`GeneratedFiles` era) was removed by the task's invalid-file deletion; `TestTemplate.t4generated.txt` and the `.T4ChangedManifest` files gained entries because this machine's full dirty set differs from the author's last run. All expected build-state churn.