# Project

- T4 code generation pipeline for Visual Studio: an MSBuild build task library (`CustomBuildTasks/`) that runs T4 templates against C++ sources incrementally, plus a C++ test bed (`T4IntegrationTestBed/`) that exercises it.
- Solution `T4IntegrationTestBed.sln` → projects `CustomBuildTasks` (C#, .NET Framework 4.7.2) and `T4IntegrationTestBed` (C++ console, v143, Win32/x64).
- Build integration (root-owned): `RunCodeGen.targets` runs `T4BuildTools.BuildT4TextFiles` in `GenerateT4Files` (BeforeTargets=PrepareForBuild) and adds `*.t4generated.h`/`*.t4generated.cpp` to the compile in `AddGeneratedFiles`; `RunCodeGen.xml` registers `*.tt` as the `TextTemplateFile` item type.
- Building or testing any change — follow the recipe in `agents/buildguild.md` (authoritative build/test instructions; current status: Goals 1.1, 2.1, and 2.2 landed — the pipeline builds end-to-end in-process, the generation core is a standalone `TemplateCompiler` API with a thin MSBuild task adapter, and a `T4CodeGen.exe` CLI front-end drives the same API).
- The CLI front-end `T4CodeGen.exe` (`T4CodeGen/`) is a second front-end over `TemplateCompiler` alongside the MSBuild task `BuildT4TextFiles`; it runs the same incremental pipeline from the command line with matching output and exit-code/failure semantics (see `T4CodeGen/README.md`).
- `t4.exe` is **not** used — the `TemplateCompiler` pipeline hosts `Mono.TextTemplating` 3.0.0 (+ in-process Roslyn) directly in `CustomBuildTasks\TemplateCompiler.cs`, driven by the thin MSBuild task adapter `BuildT4TextFiles`; no `t4` on PATH and no `powershell.exe` shell-out. Engine/Roslyn/runtime assemblies are vendored under `tools/` and referenced via HintPath.
- The checked-in `*.t4generated.*` and `*.T4ChangedManifest` files in `T4IntegrationTestBed/` are incremental build state, regenerated in place by the build.

# DOX framework

- DOX is highly performant AGENTS.md hierarchy installed here
- Agent must follow DOX instructions across any edits

## Core Contract

- AGENTS.md files are binding work contracts for their subtrees
- Work products, source materials, instructions, records, assets, and durable docs must stay understandable from the nearest applicable AGENTS.md plus every parent AGENTS.md above it

## Read Before Editing

1. Read the root AGENTS.md
2. Identify every file or folder you expect to touch
3. Walk from the repository root to each target path
4. Read every AGENTS.md found along each route
5. If a parent AGENTS.md lists a child AGENTS.md whose scope contains the path, read that child and continue from there
6. Use the nearest AGENTS.md as the local contract and parent docs for repo-wide rules
7. If docs conflict, the closer doc controls local work details, but no child doc may weaken DOX

Do not rely on memory. Re-read the applicable DOX chain in the current session before editing.

## Update After Editing

Every meaningful change requires a DOX pass before the task is done.

Update the closest owning AGENTS.md when a change affects:

- purpose, scope, ownership, or responsibilities
- durable structure, contracts, workflows, or operating rules
- required inputs, outputs, permissions, constraints, side effects, or artifacts
- user preferences about behavior, communication, process, organization, or quality
- AGENTS.md creation, deletion, move, rename, or index contents

Update parent docs when parent-level structure, ownership, workflow, or child index changes. Update child docs when parent changes alter local rules. Remove stale or contradictory text immediately. Small edits that do not change behavior or contracts may leave docs unchanged, but the DOX pass still must happen.

## Hierarchy

- Root AGENTS.md is the DOX rail: project-wide instructions, global preferences, durable workflow rules, and the top-level Child DOX Index
- Child AGENTS.md files own domain-specific instructions and their own Child DOX Index
- Each parent explains what its direct children cover and what stays owned by the parent
- The closer a doc is to the work, the more specific and practical it must be

## Child Doc Shape

- Create a child AGENTS.md when a folder becomes a durable boundary with its own purpose, rules, responsibilities, workflow, materials, or quality standards
- Work Guidance must reflect the current standards of the project or user instructions; if there are no specific standards or instructions yet, leave it empty
- Verification must reflect an existing check; if no verification framework exists yet, leave it empty and update it when one exists

Default section order:
- Purpose
- Ownership
- Local Contracts
- Work Guidance
- Verification
- Child DOX Index

## Style

- Keep docs concise, current, and operational
- Document stable contracts, not diary entries
- Put broad rules in parent docs and concrete details in child docs
- Prefer direct bullets with explicit names
- Do not duplicate rules across many files unless each scope needs a local version
- Delete stale notes instead of explaining history
- Trim obvious statements, repeated rules, misplaced detail, and warnings for risks that no longer exist

## Closeout

1. Re-check changed paths against the DOX chain
2. Update nearest owning docs and any affected parents or children
3. Refresh every affected Child DOX Index
4. Remove stale or contradictory text
5. Run existing verification when relevant
6. Report any docs intentionally left unchanged and why

## User Preferences

When the user requests a durable behavior change, record it here or in the relevant child AGENTS.md

## Child DOX Index

- `CustomBuildTasks/` — C# build-task library whose core is a standalone, MSBuild-independent `TemplateCompiler` pipeline (`TemplateCompiler.cs`) driven by the thin MSBuild task adapter `BuildT4TextFiles.cs`, plus `FileScanUtility.cs`, `AddMatchingFilesToOutput.cs`, `CustomBuildTasks.csproj`, `Debug.testproj`. See `CustomBuildTasks/AGENTS.md`.
- `T4IntegrationTestBed/` — C++ Visual Studio test bed exercising T4 templates, generated outputs, and the MSBuild integration (`T4IntegrationTestBed.vcxproj`, `T4Templates/`). See `T4IntegrationTestBed/AGENTS.md`.
- `T4CodeGen/` — console CLI front-end over the standalone `TemplateCompiler` API (`T4CodeGen.csproj`, `Program.cs`); a second front-end beside the MSBuild task that runs the same pipeline from the command line (`README.md` has usage). See `T4CodeGen/AGENTS.md`, `agents/plans/design.md`, and `T4CodeGen/README.md`.
- `agents/` — OMP-native skills location (`agents/skills/<name>/SKILL.md`, non-recursive); skills vendored from `mattpocock/skills`, per-skill invocation modes recorded in the child doc. See `agents/AGENTS.md`.
- `tools/` — vendored third-party assemblies consumed by `CustomBuildTasks` via HintPath: `Mono.TextTemplating` 3.0.0 + `Mono.TextTemplating.Roslyn` 3.0.0 (+ Roslyn `Microsoft.CodeAnalysis*` and `System.*` runtime deps), one folder per `package\version`, with license files. Keep committed — the standalone build's only dependency copies. `.gitignore` whitelists `tools/**` (negation) so the machine-global `*.dll` ignore rule cannot exclude the vendored binaries.
- Root-owned files: `RunCodeGen.targets`, `RunCodeGen.xml`, `T4IntegrationTestBed.sln`, `LICENSE`, `.gitignore`.
