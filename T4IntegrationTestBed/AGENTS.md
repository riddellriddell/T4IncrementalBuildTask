# T4IntegrationTestBed

## Purpose

C++ Visual Studio console test bed exercising the T4 code generation pipeline end to end: seed sources, T4 templates, generated outputs, and the MSBuild integration.

## Ownership

- Owned by this folder; wired into the solution by the root `T4IntegrationTestBed.sln` alongside `CustomBuildTasks`.
- The build integration contract lives at the root: `RunCodeGen.targets` + `RunCodeGen.xml`.

## Local Contracts

- `T4IntegrationTestBed.vcxproj` — console app, toolset v143, Debug/Release × Win32/x64.
  - Imports `..\RunCodeGen.targets`; registers `T4Templates\*.tt` as `TextTemplateFile` items (consumed by `GenerateT4Files`).
  - Seed sources: `Main.cpp`, `FancyWrite.cpp`, `FancyWrite.h`. `FancyWrite.h` carries the `T4Gen_RUN_TEXT_TEMPLATE_ON_THIS(TestAttribute)` tag consumed by `HeaderExample.tt`.
- `T4Templates/` — templates and shared helpers; the template parameter and marker conventions are documented in `T4Templates/AGENTS.md`.
- Generated outputs are checked in as incremental build state: `*.t4generated.h`, `*.t4generated.txt`, plus the `*.T4ChangedManifest` files. `GenerateT4Files` regenerates them in place (intermediates land in `$(Platform)\$(Configuration)\obj\GeneratedFiles` first). The stale legacy `TestTemplate.t4generated.text` file was removed automatically by the task's invalid-file cleanup on the first in-process (Goal 1.1) build.
- `RunCodeGen.targets` / `RunCodeGen.xml` inside this folder are unused legacy prototypes (early `t4 -v` + `CustomBuild` approach); the active file is the root `..\RunCodeGen.targets`.

## Work Guidance

## Verification

- Build `T4IntegrationTestBed.sln` (MSBuild or Visual Studio): `GenerateT4Files` runs before `PrepareForBuild`, then `AddGeneratedFiles` adds `*.t4generated.h` to `ClInclude` and `*.t4generated.cpp` to `Compile`; the app must compile and run.
- Dirty-detection check: edit a seed source, `.tt`, or `.ttinclude`, rebuild, and confirm the corresponding `*.t4generated.*` files regenerate.

## Child DOX Index

- `T4Templates/` — T4 templates and shared `ttinclude` helpers; template parameter and marker conventions. See `T4Templates/AGENTS.md`.
