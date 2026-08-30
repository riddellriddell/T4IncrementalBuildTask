# Build Guild

## Purpose

How an agent builds and tests this project so every change is verified before it counts as done. This file is the single source of truth for build/test commands; the AGENTS.md docs own the contracts and pipeline internals.

## Current Status: Builds End-to-End In-Process (Goal 1.1 Complete)

Milestone 1 / Goal 1.1 landed (2026-08-31): the task runs templates in-process via vendored `Mono.TextTemplating` 3.0.0 + Roslyn (`tools\`), with no `t4.exe`, no `powershell.exe`, and no PATH requirement. Deliverable 4 (per-template failure semantics = clean partial outputs + log error + continue + return `false`; plan `GOAL_1_1_failure-semantics.md`) landed too — a failing template removes its partial outputs, logs an MSBuild error, lets remaining templates run, and makes the task return `false` so the build fails. The full pipeline below builds and the app runs.

## Prerequisites

- Windows + Visual Studio 2022 with the C++ v143 toolset and .NET Framework 4.7.2 developer tools.
- `msbuild` is usually not on PATH. Run from a Developer PowerShell / VsDevCmd prompt, or call MSBuild by full path: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`.
- No `t4` on PATH, no NuGet restore, no network: the engine + Roslyn runtime assemblies are vendored under `tools\` and referenced by HintPath.

## Canonical Build & Verify Flow (target state: after Goal 1.1)

Order matters: root `RunCodeGen.targets` hardcodes `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll`, so build the library (Debug) before any solution build — including Release.

1. **Build the task library.** `msbuild CustomBuildTasks.csproj`
   Done when: `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` exists and the build reports no errors.
2. **Build the test bed solution.** `msbuild T4IntegrationTestBed.sln`
   Done when: `GenerateT4Files` runs before `PrepareForBuild` and regenerates the `*.t4generated.*` outputs; `AddGeneratedFiles` adds them to the compile; the app links.
3. **Run the app.** `T4IntegrationTestBed\Debug\T4IntegrationTestBed.exe` (x64: `T4IntegrationTestBed\x64\Debug\T4IntegrationTestBed.exe`)
   Done when: it prints the expected output — the `FancyWrite::WriteHello()` line and `TestAttribute = 420`.
4. **Confirm regeneration when you changed sources/templates.** Touch a seed source (e.g. `T4IntegrationTestBed\FancyWrite.h`) or a `.tt`/`.ttinclude`, rebuild, and check the matching `*.t4generated.*` files updated.

## Verification Bar

A change works when: the library builds clean, the solution builds, the app runs with the expected output, and — where relevant — generated files regenerate on a touched input.

## Building a Fresh Clone

The `.sln` has no project-dependency ordering (the test bed lists before `CustomBuildTasks`), so a solution build from a wiped library `bin` fails `MSB4062` (task dll not yet present). Always do step 1 first:

1. Build the library, then
2. the test bed (buildguild flow below). After the library exists, subsequent `msbuild T4IntegrationTestBed.sln` runs are fine.

## Gotchas

- `RunCodeGen.targets` (root) loads the Debug DLL by hardcoded path — moving the output or building only Release breaks the solution's `UsingTask`.
- Checked-in `*.t4generated.*` and `*.T4ChangedManifest` files are incremental build state, regenerated in place; do not hand-edit or delete.
- **In-proc DLL lock (MSB3027):** `RunCodeGen.targets` loads `CustomBuildTasks.dll` into the same MSBuild process that recompiles that project when it is out of date. Building the `.sln` with `/m` from a C#-dirty state fails to copy the DLL. Fix: build the library first (it becomes up-to-date), or drop `/m`.
- **Template language version:** the engine's compiler defaults to C# 5; templates using interpolated strings must keep `langversion="latest"` on their `<#@ template #>` directive (both current templates have it).
- **Sln ordering:** `T4IntegrationTestBed.sln` declares no dependency from the test bed on `CustomBuildTasks`; from a clean checkout build the library first (see "Building a Fresh Clone").
- `tools\` is deliberately committed (vendored engine/Roslyn/runtime assemblies). Keep them tracked; they are the standalone build's only dependency copies.
- Stale/legacy files to ignore: `T4IntegrationTestBed\RunCodeGen.targets` + `RunCodeGen.xml`, `CustomBuildTasks\Debug.testproj`, `TestTemplate.t.T4ChangedManifest`, and the empty `*.txt` template leftovers. (`TestTemplate.t4generated.text` was removed automatically by invalid-file cleanup in the Goal 1.1 build.)

## Future

- A root `test.bat` runner is planned but not present yet; wire it into this guild's flow when it lands.

## References

- Pipeline contracts and internals: `CustomBuildTasks/AGENTS.md`, `T4IntegrationTestBed/AGENTS.md`, `T4IntegrationTestBed/T4Templates/AGENTS.md`.
- Replacement milestone: `agents/plans/goals1.md` (Milestone 1, Goal 1.1).