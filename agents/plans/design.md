# Design

## Overview

This project builds a T4 code generation pipeline for Visual Studio. It drives T4 text templates against C++ seed sources to emit C++ headers and source files, runs the generation automatically as part of the build, and wires the generated files straight back into the compile so the whole pipeline is one command: build.

The pipeline is incremental — it tracks which templates and source inputs changed and only re-runs the templates that actually need it, which keeps builds fast for everything but the touched inputs.

## Core Technology

- **MSBuild** — the orchestrator. A targets file (`RunCodeGen.targets`) hooks generation before `PrepareForBuild` and adds the generated files to the compile as the build runs.
- **A C# library** (`CustomBuildTasks`, assembly `CustomBuildTasks.dll`, namespace `T4BuildTools`) whose core is a standalone, MSBuild-independent `TemplateCompiler` class that performs the incremental scan (dirty detection against a manifest), runs the templates, and copies fresh outputs into place while skipping identical files to avoid needless rebuilds. Since Goal 2.1 the MSBuild task `BuildT4TextFiles` is a thin adapter over that API; since Goal 2.2 the same API is also driven by a CLI `.exe` front-end, `T4CodeGen.exe` (`T4CodeGen/`).
- **T4** — the text template engine. Templates are `.tt` files processed **in-process** by the vendored `Mono.TextTemplating` 3.0.0 engine hosted directly in `TemplateCompiler` (`Mono.TextTemplating.Roslyn` provides the in-process Roslyn compiler via `UseInProcessCompiler()`), with `ttinclude` helpers for shared logic. Dependency tracking is comment-based: generated files embed `T4Gen_TemplateFile(...)` / `T4Gen_InputFile(...)` markers the core uses to validate cached outputs.
- **C++ console test bed** (`T4IntegrationTestBed`, v143) — exercises the pipeline end to end with seed sources, templates, and checked-in generated output.

## Standalone Intent

The project is designed to be **standalone**: a self-contained, portable code-generation step that operates purely on the files it is given (a set of seed inputs, T4 templates, and output paths) with no coupling to the surrounding application's build.

**The generation core is host-agnostic.** Since Goal 2.1 the whole pipeline lives in the standalone `TemplateCompiler` API (no `Microsoft.Build.*` references): the MSBuild task hosts it inside the build, and the same API is reachable from any other front-end (the `T4CodeGen.exe` CLI, scripts, tests) with identical results. All diagnostics flow through a single log sink; each host renders them through its own channel.

**The transformation toolchain is standalone.** Running the templates needs no Visual Studio/MSVC install, no `t4.exe`, and no `PATH` entry: `Mono.TextTemplating` 3.0.0 plus its in-process Roslyn compiler are vendored under `tools\` and loaded from `CustomBuildTasks\bin\Debug\` at build time. The only machine-level toolchain still required is whatever builds the library itself (a .NET Framework 4.7.2 MSBuild/C# toolchain, present with Visual Studio or a standalone .NET Framework targeting pack).

## Role in a Build Pipeline

This is a **build-pipeline step**, not an application. It slots in as:

```
seed sources + .tt templates  →  [T4 generation]  →  generated .h/.cpp  →  compile
```

The generated files land as real build inputs (`*.t4generated.h` / `*.t4generated.cpp` are added to the compile), so downstream steps treat them like any handwritten source. Because the whole step is driven by MSBuild, it composes with other pipeline steps, respects incremental builds, and can be inserted into any Visual Studio-based pipeline that needs code generated from T4 templates on demand.