# Build Guild

## Purpose

How an agent builds and tests this project so every change is verified before it counts as done. This file is the single source of truth for build/test commands; the AGENTS.md docs own the contracts and pipeline internals.

## Current Status: End-to-End Build Blocked

The pipeline does **not** build end-to-end today:

- The task shells out to the VS-installed `t4.exe` via `powershell.exe`, and `t4` is **not installed** (or on PATH) on this machine.
- `t4` is being **replaced**, not installed: the in-process Mono.TextTemplating engine swap — **Milestone 1, Goal 1.1** in `agents/plans/goals1.md` — must be completed **first** before building works.

Until Goal 1.1 lands, the only reachable build is the task library on its own (see "Reachable now"). Do not attempt the solution build and expect success.

## Prerequisites

- Windows + Visual Studio 2022 with the C++ v143 toolset and .NET Framework 4.7.2 developer tools.
- `msbuild` is usually not on PATH. Run from a Developer PowerShell / VsDevCmd prompt, or call MSBuild by full path: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`.
- After Goal 1.1 there is no `t4`/PATH requirement; before it, end-to-end builds are blocked regardless.

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

## Reachable Now

- `msbuild CustomBuildTasks.csproj` works today (plain C# build; no `t4` involved).
- Everything else in the flow is blocked on Goal 1.1 (see Current Status).

## Gotchas

- `RunCodeGen.targets` (root) loads the Debug DLL by hardcoded path — moving the output or building only Release breaks the solution's `UsingTask`.
- Checked-in `*.t4generated.*` and `*.T4ChangedManifest` files are incremental build state, regenerated in place; do not hand-edit or delete.
- `ChangeFileMainfest` (sic) is a misspelling baked into the task's `t4` command line and the templates' `<#@ parameter #>` declarations; Goal 1.1 renames it to `ChangeFileManifest`. Templates that declare the parameter must keep the current spelling until that lands.
- Stale/legacy files to ignore: `T4IntegrationTestBed\RunCodeGen.targets` + `RunCodeGen.xml`, `CustomBuildTasks\Debug.testproj`, `T4IntegrationTestBed\TestTemplate.t4generated.text`, `TestTemplate.t.T4ChangedManifest`, and the empty `*.txt` template leftovers.

## Future

- A root `test.bat` runner is planned but not present yet; wire it into this guild's flow when it lands.

## References

- Pipeline contracts and internals: `CustomBuildTasks/AGENTS.md`, `T4IntegrationTestBed/AGENTS.md`, `T4IntegrationTestBed/T4Templates/AGENTS.md`.
- Replacement milestone: `agents/plans/goals1.md` (Milestone 1, Goal 1.1).