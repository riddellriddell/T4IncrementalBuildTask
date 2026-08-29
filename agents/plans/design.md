# Design

## Overview

This project builds a T4 code generation pipeline for Visual Studio. It drives T4 text templates against C++ seed sources to emit C++ headers and source files, runs the generation automatically as part of the build, and wires the generated files straight back into the compile so the whole pipeline is one command: build.

The pipeline is incremental — it tracks which templates and source inputs changed and only re-runs the templates that actually need it, which keeps builds fast for everything but the touched inputs.

## Core Technology

- **MSBuild** — the orchestrator. A targets file (`RunCodeGen.targets`) hooks generation before `PrepareForBuild` and adds the generated files to the compile as the build runs.
- **A C# MSBuild build task** (`CustomBuildTasks`, assembly `CustomBuildTasks.dll`, namespace `T4BuildTools`) — performs the incremental scan (dirty detection against a manifest), runs the templates, and copies fresh outputs into place while skipping identical files to avoid needless rebuilds.
- **T4** — the text template engine. Templates are `.tt` files processed by `t4.exe` (invoked through `powershell.exe`), with `ttinclude` helpers for shared logic. Dependency tracking is comment-based: generated files embed `T4Gen_TemplateFile(...)` / `T4Gen_InputFile(...)` markers the task uses to validate cached outputs.
- **C++ console test bed** (`T4IntegrationTestBed`, v143) — exercises the pipeline end to end with seed sources, templates, and checked-in generated output.

## Standalone Intent

The project is designed to be **standalone**: a self-contained, portable code-generation step that operates purely on the files it is given (a set of seed inputs, T4 templates, and output paths) with no coupling to the surrounding application's build.

It is **not standalone today**. The toolchain dependency on MSVC (v143) is inherited through two things:

1. `t4.exe` ships with Visual Studio, so running the templates requires a Visual Studio/MSVC installation to be present, and
2. `t4` must be on `PATH` for the task to launch it.

Removing that coupling — for example by bundling a standalone T4 engine (such as Mono.TextTemplating) so the task no longer shells out to the VS-installed `t4.exe` — is a stated direction and would make the component genuinely standalone.

## Role in a Build Pipeline

This is a **build-pipeline step**, not an application. It slots in as:

```
seed sources + .tt templates  →  [T4 generation]  →  generated .h/.cpp  →  compile
```

The generated files land as real build inputs (`*.t4generated.h` / `*.t4generated.cpp` are added to the compile), so downstream steps treat them like any handwritten source. Because the whole step is driven by MSBuild, it composes with other pipeline steps, respects incremental builds, and can be inserted into any Visual Studio-based pipeline that needs code generated from T4 templates on demand.