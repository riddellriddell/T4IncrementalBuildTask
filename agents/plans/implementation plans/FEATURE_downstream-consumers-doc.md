# Implementation Plan: Downstream-Consumer Documentation and Third-Party Notices

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `FEATURE`
- Task Name: Downstream-consumer documentation (`docs/DOWNSTREAM-CONSUMERS.md`) + aggregated third-party notices (`THIRD-PARTY-NOTICES.md`)
- Status: `Draft`
- Owner: "Your Name"
- Last Updated: `2026-09-07`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [buildguild.md](../../buildguild.md)
- Milestone: [milestones.md](../milestones.md) (post-Milestone-2 housekeeping/hardening)
- Goal: [goals2.md](../goals2.md) (Goal 2.1/2.2 landed; this is follow-on documentation work, not a deliverable gate)
- Handover: n/a

## Objective

Ship `docs/DOWNSTREAM-CONSUMERS.md` — an authoritative, per-consumption-model file manifest that states, without guesswork, exactly which repository files a downstream project must copy or vendor for each of the two products:

1. the standalone `T4CodeGen.exe` (source build and prebuilt-artifact variants), and
2. the MSBuild task (`CustomBuildTasks.dll` + `RunCodeGen.targets`/`RunCodeGen.xml`).

Plus the extra-credit deliverable, a root-level `THIRD-PARTY-NOTICES.md` that aggregates the 12 vendored NuGet packages under `tools/` (package, version, DLL, license attribution) for license/compliance handling when a consumer redistributes the vendored binaries.

## Problem Summary

The repository mixes two products in one tree:

- the **MSBuild-task product** — `CustomBuildTasks/` (assembly + `TemplateCompiler` pipeline), root-owned `RunCodeGen.targets`/`RunCodeGen.xml`, the C++ test bed `T4IntegrationTestBed/`, and internal tooling `agents/`, `.idea/`, `test.bat`;
- the **standalone-exe product** — `T4CodeGen/` (`T4CodeGen.exe` CLI) and its black-box harness `T4CodeGenTests/`, both built offline from the same vendored `tools/`.

Downstream consumers must "pick out pieces" with no authoritative map: the only folder descriptions live in the root `AGENTS.md` Child DOX Index, which is written for repository maintainers, not consumers, and it describes folders rather than per-consumption-model file manifests. Nothing states "to consume the exe you need these N files; to consume the task you need these M files." Separately, the vendored binary set under `tools/` has no aggregated compliance record — license texts are scattered one-per-package (`LICENSE`/`LICENSE.TXT`), and two packages (`microsoft.codeanalysis.common` 4.7.0, `microsoft.codeanalysis.csharp` 4.7.0) ship **no vendored license file at all**, leaving a redistribution/generated-binary compliance hole.

The "move each product to its own branch/repo" option is real but heavyweight; this plan deliberately lands the low-cost scalar documentation first and records the split as a possible future move.

## Scope

- In scope: new `docs/DOWNSTREAM-CONSUMERS.md` — a per-product file manifest (standalone exe vs MSBuild task), recommended vendor folder layout for each, a "what you explicitly do NOT need" section, note on retaining the repo MIT `LICENSE`, and a forward-pointer anticipating the GitHub-Releases prebuilt-artifact consumption model.
- In scope: new root-level `THIRD-PARTY-NOTICES.md` — one row per `tools/` package (12 total), enumerating package, version, contained DLL, and license attribution, aggregating/adjusting for the two Roslyn packages that lack vendored license text.
- In scope: cross-links from `T4CodeGen/README.md` and `T4CodeGen/AGENTS.md` to `docs/DOWNSTREAM-CONSUMERS.md`, plus a `docs/` entry in the root `AGENTS.md` Child DOX Index.
- In scope: note the branch/repo-split as an explicitly-deferred future option inside `docs/DOWNSTREAM-CONSUMERS.md` and in this plan's Risks.
- Out of scope: actually moving code between branches/repos, changing the build, or touching product source (`CustomBuildTasks/`, `T4CodeGen/`), tests, or `T4IntegrationTestBed.sln`.
- Out of scope: the separate GitHub-Releases prebuilt-artifact plan — this doc only anticipates consumers fetching the artifact instead of building from source once that lands.
- Out of scope: generating `THIRD-PARTY-NOTICES.md` from a build-time tool (see Open Questions).

## Current State

- Mixed-product tree confirmed: root children are `CustomBuildTasks/`, `T4IntegrationTestBed/`, `T4CodeGen/`, `T4CodeGenTests/`, `agents/` (incl. `agents/plans/`), `tools/`, `.idea/`, plus root-owned `RunCodeGen.targets`, `RunCodeGen.xml`, `T4IntegrationTestBed.sln`, `LICENSE`, `.gitignore`, `test.bat`.
- No `docs/` folder and no `THIRD-PARTY-NOTICES.md` exist anywhere (verified). The root `AGENTS.md` Child DOX Index is the only existing map, and it is folder-level, not consumption-level.
- `tools/` holds 12 package folders, one per `package\version`, each a DLL + (mostly) a license file:
  - `mono.texttemplating\3.0.0\` (`Mono.TextTemplating.dll`, `LICENSE`)
  - `mono.texttemplating.roslyn\3.0.0\` (`Mono.TextTemplating.Roslyn.dll`, `LICENSE`)
  - `microsoft.codeanalysis.common\4.7.0\` (`Microsoft.CodeAnalysis.dll`, **no license file**)
  - `microsoft.codeanalysis.csharp\4.7.0\` (`Microsoft.CodeAnalysis.CSharp.dll`, **no license file**)
  - `system.buffers\4.5.1\` — `system.collections.immutable\7.0.0\` — `system.memory\4.5.5\` — `system.numerics.vectors\4.5.0\` — `system.reflection.metadata\7.0.0\` — `system.runtime.compilerservices.unsafe\6.0.0\` — `system.text.encoding.codepages\7.0.0\` — `system.threading.tasks.extensions\4.5.4\` (each one `*.dll` + `LICENSE.TXT`). All 12 packages are MIT-licensed upstream.
- `T4CodeGen/README.md` documents the exe's CLI for a repository developer but contains no file manifest for downstream vendoring; both `.csproj` files (`CustomBuildTasks.csproj`, `T4CodeGen.csproj`) reference the same 12-assembly HintPath set under `tools/`, and `T4CodeGen.csproj` `ProjectReference`s the whole `CustomBuildTasks.csproj`.
- Note: `T4IntegrationTestBed\RunCodeGen.targets` is a **stale echo-only prototype** (`<Exec Command="echo Compiling with exec command ..."/>`); the real integration is the root `RunCodeGen.targets` + `RunCodeGen.xml`, imported by the vcxproj as `..\RunCodeGen.targets`. The consumer doc must point at the root pair.
- `.gitignore` does not ignore `docs/` or `*.md`, so the new files will be tracked and committed (confirmed desired).

## Assumptions and Constraints

- The deliverable is the documentation, not branch/repo surgery; any physical split is explicitly deferred and only *noted* as a future option.
- The consumer doc must mirror the root `AGENTS.md` Child DOX Index (the authoritative folder map) so the two never contradict each other.
- DOX rules apply: creating `docs/` and `THIRD-PARTY-NOTICES.md` is a durable structure change, so the owning docs must be updated in the same change — root `AGENTS.md` (Child DOX Index + root-owned list), `T4CodeGen/AGENTS.md`, and `agents/plans/AGENTS.md` (plan index).
- License facts are taken from the vendored files: 10 packages carry `LICENSE`/`LICENSE.TXT`; the two Roslyn packages carry DLLs only. The notices doc must not invent text where no license file exists — it must either embed the MIT notice for those two with an explicit "no vendored license file" flag or the implementer first vendors the official license texts (see Open Questions #2).
- Both products are .NET Framework 4.7.2 and build fully offline from `tools/` only; the manifest assumes the consumer already has MSBuild/VS installed and needs no network.
- The exe source-build model necessarily vendors the whole `CustomBuildTasks/` project (the exe `ProjectReference`s it), even though only `TemplateCompiler.cs` is the API surface it uses at runtime.

## Files and Areas Likely Affected

- `docs/DOWNSTREAM-CONSUMERS.md` — new (this is the deliverable): per-consumption-model file manifest, vendor layouts, do-not-need list, license retention notes, GitHub-Releases anticipation note.
- `THIRD-PARTY-NOTICES.md` — new (root): aggregated 12-package attribution table + top-level MIT notice for the repo itself.
- `AGENTS.md` (root) — Child DOX Index: add a `docs/` entry (and note `THIRD-PARTY-NOTICES.md` ownership); the consumer doc must not drift from this index.
- `T4CodeGen/README.md` — add a cross-link section pointing consumers at `docs/DOWNSTREAM-CONSUMERS.md`.
- `T4CodeGen/AGENTS.md` — Ownership/Local Contracts: point at the consumer doc as the canonical vendoring story for the exe.
- `agents/plans/AGENTS.md` — `implementation plans/` index: add this plan (mark `Landed <date>` on completion).

## Implementation Steps

1. **Inventory the two consumption models from in-repo data.** Derive the exe model's file set from `T4CodeGen\T4CodeGen.csproj` (Compile items + the 12 HintPath references + `ProjectReference` to `CustomBuildTasks.csproj`) and the task model's from `RunCodeGen.targets`/`RunCodeGen.xml` + `CustomBuildTasks\CustomBuildTasks.csproj`. Record the root `AGENTS.md` Child DOX Index as the folder-level authority the manifest must mirror. Confirm each listed file exists (`Test-Path` list-vs-glob) before it enters the manifest.
2. **Draft `docs/DOWNSTREAM-CONSUMERS.md`.** Structure:
   - One-line statement of intent: this is the file-consumption map; downstream projects copy only what their model needs.
   - **Model A — standalone `T4CodeGen.exe`:**
     - *Source build:* `T4CodeGen\T4CodeGen.csproj`, `Program.cs`, `Properties\AssemblyInfo.cs`; the whole `CustomBuildTasks\` project (`CustomBuildTasks.csproj`, `TemplateCompiler.cs`, `BuildT4TextFiles.cs`, `FileScanUtility.cs`, `AddMatchingFilesToOutput.cs`, `Properties\AssemblyInfo.cs`); the 12 `tools\` DLLs; repo `LICENSE`; `THIRD-PARTY-NOTICES.md`. Build: `msbuild T4CodeGen\T4CodeGen.csproj` (offline). Note `AddMatchingFilesToOutput.cs` and `Debug.testproj` are compiled-but-unreferenced/stale and are optional/reference-only in a trimmed copy.
     - *Prebuilt artifact:* the `T4CodeGen\bin\Debug\` output folder (`T4CodeGen.exe` + `CustomBuildTasks.dll` + the 12 runtime DLLs) — no project files needed; anticipate the GitHub-Releases artifact replacing this once that plan lands.
     - Recommended vendor layout (exe copy): `vendor\T4CodeGen\` tree with the project, `CustomBuildTasks\`, `tools\<package>\<version>\`, and the two doc files.
   - **Model B — MSBuild task:**
     - `RunCodeGen.targets` + `RunCodeGen.xml` (root pair — *not* the stale `T4IntegrationTestBed\RunCodeGen.targets`), the `CustomBuildTasks\` project, the 12 `tools\` DLLs, `LICENSE`, `THIRD-PARTY-NOTICES.md`. Build: `msbuild CustomBuildTasks\CustomBuildTasks.csproj` → `CustomBuildTasks\bin\Debug\CustomBuildTasks.dll` (the exact path the targets load). `RunCodeGen.xml` registers `*.tt` as `TextTemplateFile`; the consumer project imports `..\RunCodeGen.targets` like `T4IntegrationTestBed.vcxproj` does.
   - **What neither model needs:** `T4IntegrationTestBed/` (validation example only), `T4CodeGenTests/` (internal black-box harness), `agents/`, `.idea/`, `test.bat`, `T4IntegrationTestBed.sln`, `x64/` outputs, checked-in `*.t4generated.*`/`*.T4ChangedManifest` build state.
   - **Compliance:** keep the MIT `LICENSE` with any redistribution; the vendored-binaries attribution lives in `THIRD-PARTY-NOTICES.md`.
   - **Future note:** branch/repo split is a possible later move; if it happens, this doc dies and each product carries its own manifest. GitHub-Releases artifact consumption supersedes the source-build manifests once the separate release plan lands.
3. **Enumerate `tools/` into root `THIRD-PARTY-NOTICES.md`.** Table of 12 rows: package / version / contained DLL / license (MIT) / vendored license file? (10 yes — `LICENSE` or `LICENSE.TXT`; 2 no — `microsoft.codeanalysis.common` 4.7.0 and `microsoft.codeanalysis.csharp` 4.7.0). Embed the MIT notice text once at top; for the two Roslyn rows either (a) embed MIT with an explicit "no vendored license file in this repo — see https://github.com/dotnet/roslyn LICENSE" note, or (b) first vendor the official license files under their package folders (decide per Open Questions #2). Add the repo's own MIT `LICENSE` reference for complete attribution.
4. **Wire cross-links.** In `T4CodeGen/README.md` add a "Consuming the exe in your own project" pointer to `docs/DOWNSTREAM-CONSUMERS.md`; in `T4CodeGen/AGENTS.md` add the same pointer under Ownership/Local Contracts; in root `AGENTS.md` add a `docs/` Child DOX Index entry naming both new files.
5. **DOX pass (Update After Editing).** Update root `AGENTS.md` (new `docs/` entry + root-owned `THIRD-PARTY-NOTICES.md` mention), `T4CodeGen/AGENTS.md` and `T4CodeGen/README.md` (cross-links), and `agents/plans/AGENTS.md` (add this plan to the `implementation plans/` index, flipping its status line to `Landed <date>` once complete).

## Verification Plan

### Automated Checks

- Consistency check: a PowerShell one-liner that extracts every `Explanation path` listed in `docs/DOWNSTREAM-CONSUMERS.md` and asserts each exists in the repo, e.g.:
  `Select-String -Path 'docs\DOWNSTREAM-CONSUMERS.md' -Pattern '\b(?:docs|tools|T4CodeGen|CustomBuildTasks|RunCodeGen)[\\/][\w\\.\\/-]+' -AllMatches | ... | ForEach-Object { if (-not (Test-Path -LiteralPath $_)) { throw "missing $_" } }` — must complete without a missing path.
- Notices/directory parity check: `(Get-ChildItem tools -Directory).Count -eq 12` and every `package\version` row in `THIRD-PARTY-NOTICES.md` matches an actual folder under `tools\`.
- Cross-link check: grep confirms `docs/DOWNSTREAM-CONSUMERS.md` is referenced from root `AGENTS.md` and `T4CodeGen/README.md`.
- Existing regression gate unchanged: root `test.bat` still passes (doc-only change must not break the build/test bed).

### Manual Checks

1. Assemble a scratch consumer folder using **only the Model A manifest** files; run `msbuild T4CodeGen.csproj` offline (no network); run the built `T4CodeGen.exe -h` from the project dir → help printed and exit code `0`.
2. Assemble a scratch consumer folder using **only the Model B manifest** files; build a minimal project importing `RunCodeGen.targets` (mirroring `T4IntegrationTestBed.vcxproj` line 149's `..\RunCodeGen.targets` import) with one `*.tt` → the `GenerateT4Files` target runs the task and emits the generated file with no `t4` on PATH.
3. Confirm `THIRD-PARTY-NOTICES.md` row count equals the `tools\` directory count (12) and that the two Roslyn rows' license treatment matches what actually exists in `tools\`.

## Risks and Open Questions

- Risk: **doc drift** — the manifest goes stale when product files change. Mitigation: the manifest mirrors the root `AGENTS.md` Child DOX Index (maintained by DOX passes), and the item-1 inventory runs list-vs-glob at implementation time; a follow-up could fold the existence check into `test.bat`.
- Risk: **license accuracy for the two Roslyn packages** — no vendored license file exists, so an unlabeled row could misstate the license (upstream `dotnet/roslyn` is MIT) or the obtained-from source.
- Question: **should the official MIT license files be vendored under `tools\microsoft.codeanalysis.common\4.7.0\` and `tools\microsoft.codeanalysis.csharp\4.7.0\`** to close the compliance gap at the source, rather than embedding text only in the notices doc? Small change; decide at implementation (favors vendoring, then the notices row says "vendored LICENSE"). Also do the two package folders need the rest of the NuGet metadata for a defensible attribution trail?
- Question: **should `THIRD-PARTY-NOTICES.md` be generated by a script** to guarantee freshness? Answer proposed (now): handwritten — 12 stable rows, and generation tooling is not worth the cost until `tools/` churns.
- Question: **does `docs/` deserve its own AGENTS.md?** Proposed (now): no — two files, owned from the root `AGENTS.md` enum; only create a child DOX doc if `docs/` grows beyond a couple of files.
- Dependency: none external; all verification is offline against the repo itself.

## Completion Checklist

- [ ] Implementation matches the linked design and goal context (doc reflects the Child DOX Index + standalone-exe intent from `design.md`)
- [ ] Scope stayed within this plan (docs + cross-links only; no source/build/sln changes, no branch surgery)
- [ ] Verification steps were completed or explicitly deferred (existence check, notices parity, scratch-folder manual runs, `test.bat` regression)
- [ ] Relevant status docs were updated (root `AGENTS.md`, `T4CodeGen/AGENTS.md` + `README.md`, `agents/plans/AGENTS.md`)
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Read `agents/buildguild.md` before manual verification; the canonical offline build/verify flow is library → exe → test bed.
- The exe model vendors **both** projects (`T4CodeGen` + `CustomBuildTasks`) because of the `ProjectReference`; the task model needs neither `T4CodeGen/` nor `T4CodeGenTests/`. Do not collapse these in the manifest.
- The authoritative MSBuild integration files are the **root** `RunCodeGen.targets`/`RunCodeGen.xml`; the `T4IntegrationTestBed\RunCodeGen.targets` copy is a stale echo-only prototype and must be flagged, not recommended.
- The assembly list is identical across both products — the same 12 `tools\` HintPaths appear in both `.csproj` files; the manifest should state that the runtime DLL set is `Mono.TextTemplating`, `Mono.TextTemplating.Roslyn`, `Microsoft.CodeAnalysis`, `Microsoft.CodeAnalysis.CSharp`, `System.Buffers`, `System.Collections.Immutable`, `System.Memory`, `System.Numerics.Vectors`, `System.Reflection.Metadata`, `System.Runtime.CompilerServices.Unsafe`, `System.Text.Encoding.CodePages`, `System.Threading.Tasks.Extensions`.
- Do not invent files: every manifest row must trace to a real path (inventoried in step 1) or to a generated/build output (`bin\Debug\...`), which the doc must label as build outputs, not source.
- Requirement to keep in the doc: consumers must retain the MIT `LICENSE` and the `THIRD-PARTY-NOTICES.md` alongside any vendored copy of the products or their binaries.