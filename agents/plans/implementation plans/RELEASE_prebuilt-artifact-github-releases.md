# Implementation Plan: Prebuilt `T4CodeGen` Release Artifact via GitHub Releases

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `RELEASE`
- Task Name: Prebuilt `T4CodeGen` release artifact (self-contained zip) via GitHub Releases
- Status: `Draft`
- Owner: "Your Name"
- Last Updated: `2026-09-07`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [agenticworkflow.md](../agenticworkflow.md)
- Goal: (standalone release plumbing — not tied to a numbered milestone goal)

## Objective

Publish a self-contained zip artifact (`T4CodeGen-win-x64-<version>.zip`) containing `T4CodeGen.exe` plus all engine/Roslyn runtime DLLs to GitHub Releases with semantic version tags, so downstream consumers can fetch the zip and use the tool without vendoring source, running MSBuild, or editing `.gitignore`/csproj.

## Problem Summary

Today, a downstream consumer who wants the T4 code-generation pipeline must:

1. Copy the entire repo (or submodule it) — source for `CustomBuildTasks`, `T4CodeGen`, the vendored `T4CodeGen\tools\` DLLs, the test bed, and all DOX docs.
2. Run `msbuild CustomBuildTasks.csproj` then `msbuild T4CodeGen\T4CodeGen.csproj` (or the full `.sln`).
3. Edit their `.gitignore` to allow `*.dll` exceptions for the vendored `T4CodeGen\tools\` copies, or add the entire `T4CodeGen\tools\` folder.
4. Optionally trim or adapt a csproj to reference the exe without pulling in unwanted project dependencies.

A prebuilt release zip eliminates every one of these steps: download, extract, put on PATH (or reference by absolute path), and run. This is the single biggest win for vendoring this project into other repositories.

## Scope

- In scope: a PowerShell packaging script (`scripts\package-release.ps1`) that builds `T4CodeGen.csproj` in Release config, gathers the exe + runtime DLLs into a flat staging directory, computes a SHA-256 checksum, and produces a versioned zip file.
- In scope: a GitHub Actions workflow (`.github\workflows\release.yml`) triggered by a semver tag push that runs the packaging script, creates a GitHub Release, and attaches the zip + checksum file as release assets.
- In scope: documented `curl`/`gh` fetch examples for downstream consumers.
- In scope: DOX updates (this plan, `T4CodeGen/README.md`, `agents/plans/AGENTS.md` index, root `AGENTS.md` Child DOX Index).
- In scope: ship **only the runtime-referenced subset** of the vendored assemblies (sub-approach (b) from `FEATURE_relocate-tools-under-t4codegen.md`) — the zip copies from `T4CodeGen\tools\` and ships only the DLLs the exe demonstrably loads at runtime (derived empirically by the stripped-copy probe in that plan's Manual Checks), **not** the full 12-package compile-time reference set. .NET Framework 4.7.2 already ships some `System.*` types in the GAC, so the runtime set is a proper subset of the 12-entry reference set; the empirical derivation belongs to this plan's verification.
- Out of scope: changing `T4CodeGen.csproj`, `CustomBuildTasks.csproj`, or any source code — the script consumes the existing build output as-is.
- Out of scope: publishing `CustomBuildTasks.dll` separately (the exe bundles what it needs via project-reference + HintPath copy; a future plan can add a library-only artifact if needed).
- Out of scope: Linux/macOS builds — the exe is .NET Framework 4.7.2 (Windows-only); the zip is `win-x64` by convention.

## Current State

- `T4CodeGen.csproj` already defines a Release configuration (`bin\Release\`, optimize on, pdb-only symbols). A Release build works offline from the vendored `T4CodeGen\tools\` tree only (hint-path reference set; the tree relocated from the repo root to `T4CodeGen/tools/` per `FEATURE_relocate-tools-under-t4codegen.md`).
- The exe's HintPath references cause MSBuild to copy the referenced engine/Roslyn/runtime DLLs beside `T4CodeGen.exe` in `bin\Release\` at build time. Which of those the exe *actually loads* at runtime is the empirical subset question resolved by the stripped-copy probe (see Scope); the compile-time reference set is the 12-package `T4CodeGen\tools\` set.
- No `.github\workflows\` folder exists in the repo. The plan adds one.
- `git tag -l` returns empty — no semantic tags exist yet. The first release will be `v1.0.0`.
- Remote: `origin` → `github.com/riddellriddell/T4IncrementalBuildTask.git`.
- `.gitignore` already ignores `[Rr]elease/`, `[Bb]in/`, and `[Oo]bj/` — the Release build output is git-ignored, which is correct (artifacts ship via GitHub Releases, not git).
- The root `test.bat` harness can validate the release zip independently (point the harness at the extracted exe).

## Assumptions and Constraints

- .NET Framework 4.7.2 exe runs on Windows only; the zip is Windows-targeted. Consumers on other platforms cannot use it.
- "Self-contained" means all runtime DLLs in the same directory as the exe, not .NET single-file publish — the exe loads adjacent DLLs at startup.
- The build must be offline (no NuGet restore, no network) — all dependencies are vendored under `tools\`.
- The packaging script must run from a Developer PowerShell / VsDevCmd prompt (or have `msbuild` on PATH).
- The zip file name follows `T4CodeGen-win-x64-<semver>.zip`; the checksum file follows `T4CodeGen-win-x64-<semver>.zip.sha256`.
- Semantic versioning applies: `v<major>.<minor>.<patch>`. The first release is `v1.0.0`.
- The Release build output directory (`T4CodeGen\bin\Release\`) is ephemeral and git-ignored; the script cleans it before building.
- The script does not sign the zip or exe (no code-signing certificate in the repo); checksum is the integrity mechanism.

## Files and Areas Likely Affected

- `scripts\package-release.ps1` — **new file**. The core packaging script: clean, build Release, stage files, zip, checksum.
- `.github\workflows\release.yml` — **new file**. GitHub Actions workflow: trigger on tag push, run the packaging script, create a GitHub Release, attach assets.
- `T4CodeGen\README.md` — update the Build section to mention the release zip as an alternative to building from source; add a "Quick Install" subsection with a `curl`/`gh` fetch example.
- `agents\plans\AGENTS.md` — add this plan to the index.
- Root `AGENTS.md` — update Child DOX Index if a `scripts/` entry is warranted (minor).
- `agents\buildguild.md` — add a note that `scripts\package-release.ps1` is the release packaging path (not part of the normal build/verify flow).

## Implementation Steps

1. **Create `scripts\package-release.ps1`.** Parameters: `-Version <semver>` (required, e.g. `1.0.0`). Steps inside the script:
   - Validate that `msbuild` is callable (try `msbuild /version`; exit with an error message if not found — tell the user to run from a Developer PowerShell).
   - Clean the Release output directory: `Remove-Item -Recurse -Force T4CodeGen\bin\Release\` if it exists.
   - Build the task library in Debug first (required by the solution build order): `msbuild CustomBuildTasks.csproj /p:Configuration=Debug /v:minimal`.
   - Build `T4CodeGen.csproj` in Release: `msbuild T4CodeGen\T4CodeGen.csproj /p:Configuration=Release /v:minimal`.
   - Verify `T4CodeGen\bin\Release\T4CodeGen.exe` exists; exit with an error if not.
   - Create a staging directory: `staging\T4CodeGen\` under the repo root (clean it first).
   - Copy `T4CodeGen\bin\Release\T4CodeGen.exe` into the staging directory.
   - Copy the runtime-referenced subset of `*.dll` files from `T4CodeGen\bin\Release\` into the staging directory — for the first release, that is the set the exe demonstrably loads, derived by the stripped-copy probe (ship only what a green run needs; the full 12-package compile-time reference set is deliberately not shipped).
   - Optionally copy `T4CodeGen.exe.config` if present (it is not currently, but future-proof).
   - Create the zip: `T4CodeGen-win-x64-<version>.zip` containing the flat `T4CodeGen\` folder (one level of nesting, not a flat zip of DLLs — so extracting produces a `T4CodeGen\` directory).
   - Compute SHA-256: write `T4CodeGen-win-x64-<version>.zip.sha256` containing `<hash>  T4CodeGen-win-x64-<version>.zip`.
   - Print the zip path, checksum, and file count to stdout for CI capture.
   - Clean up the staging directory.

2. **Create `.github\workflows\release.yml`.** Trigger: `push` of tags matching `v*.*.*`. Steps:
   - Checkout the repo at the tag ref.
   - Set up MSBuild (use `microsoft/setup-msbuild@v2` action or call VS installer paths directly).
   - Run `scripts\package-release.ps1 -Version ${{ github.ref_name }}` (strip the `v` prefix for the `-Version` parameter).
   - Create a GitHub Release via `gh release create` or the `softprops/action-gh-release` action:
     - Tag: the pushed tag.
     - Name: `T4CodeGen <version>`.
     - Body: generated from the tag message or a short template ("Self-contained build of T4CodeGen.exe with engine/Roslyn runtime DLLs. See README for usage.").
     - Assets: attach `T4CodeGen-win-x64-<version>.zip` and `T4CodeGen-win-x64-<version>.zip.sha256`.
     - Draft: false (publish immediately).

3. **Manual verification of the script (before CI is live).** Run the script locally to produce the first release zip:
   ```
   pwsh scripts\package-release.ps1 -Version 1.0.0
   ```
   Confirm: zip exists at `T4CodeGen-win-x64-1.0.0.zip`, checksum file exists, zip contains `T4CodeGen\T4CodeGen.exe` + 12 DLLs.

4. **Verify the release zip as a standalone artifact.** Extract to a clean temp directory (no repo, no `tools\`, no csproj). Run:
   ```
   .\T4CodeGen\T4CodeGen.exe -h
   ```
   Confirm: exit code `0`, usage text printed to stdout. This proves the DLLs are self-sufficient.

5. **Run the existing black-box harness against the release zip.** Build `T4CodeGenTests`, then modify the harness invocation (or run manually) to point at the extracted `T4CodeGen.exe` from the zip instead of `bin\Debug\`. All cases must still pass. (This is a manual check for the first release; a future plan can add a `-ExePath` parameter to the harness.)

6. **Tag and publish the first release.** After the local verification passes:
   ```
   git tag -a v1.0.0 -m "T4CodeGen v1.0.0 — first prebuilt release"
   git push origin v1.0.0
   ```
   This triggers the GitHub Actions workflow, which builds, zips, and publishes the release with assets.

7. **Update `T4CodeGen\README.md`.** Add a "Quick Install (Prebuilt)" section before or after the Build section:
   ```markdown
   ## Quick Install (Prebuilt)

   Download the latest release zip from [GitHub Releases](https://github.com/riddellriddell/T4IncrementalBuildTask/releases):

   ```
   curl -LO https://github.com/riddellriddell/T4IncrementalBuildTask/releases/latest/download/T4CodeGen-win-x64-<version>.zip
   ```

   Extract and add the `T4CodeGen\` directory to your `PATH`, or reference `T4CodeGen.exe` by absolute path.
   ```
   Note the version in the URL; or use the `latest` redirect if GitHub supports it for the most recent release.

8. **DOX pass.** Update `agents\plans\AGENTS.md` index with this plan. Update `agents\buildguild.md` with a note about the release script (not part of the normal build/verify flow — it is a release-time operation). Update root `AGENTS.md` Child DOX Index if a `scripts/` directory entry is warranted.

## Verification Plan

### Automated Checks

- `msbuild T4CodeGen\T4CodeGen.csproj /p:Configuration=Release` — must succeed and produce `T4CodeGen\bin\Release\T4CodeGen.exe` plus the referenced engine/Roslyn/runtime DLL set.
- `pwsh scripts\package-release.ps1 -Version 1.0.0` — must produce `T4CodeGen-win-x64-1.0.0.zip` (non-zero size) and `T4CodeGen-win-x64-1.0.0.zip.sha256`.
- Zip contents check (manual one-liner or script): unzip the zip to a temp dir, confirm `T4CodeGen\T4CodeGen.exe` exists; the DLL count is the empirical runtime set (fixed after the stripped-copy probe, not the full 12).
- `T4CodeGen.exe -h` from the extracted zip directory — exit code `0`.

### Manual Checks

1. Extract the zip to a directory outside the repo (e.g. `C:\temp\release-test\T4CodeGen\`). Run `T4CodeGen.exe -h` — confirm usage text prints and exit code is `0`.
2. Run the existing `test.bat` harness with the release zip's exe (substitute the exe path) — confirm all cases pass.
3. Download the zip via the `curl` example from the README against a draft or real release — confirm the fetch URL resolves and the file is intact (checksum matches).
4. Run `T4CodeGen.exe` from the extracted zip against the test bed fixtures (copy the test bed seeds/templates to a temp dir, run with the six required args) — confirm exit `0` and generated files produced.

## Risks and Open Questions

- Risk: DLL version drift — if `tools\` assemblies are updated in the repo but the release tag is not bumped, consumers get stale DLLs. Mitigation: the packaging script always builds from the current HEAD of the tagged commit; the tag is the source of truth.
- Risk: Windows-only — the .NET Framework 4.7.2 exe cannot run on Linux/macOS. Consumers on those platforms cannot use the prebuilt zip. Mitigation: document the platform constraint; a future plan could explore .NET 6+ self-contained publish if cross-platform is needed.
- Risk: Consumers may have other versions of the same DLLs (e.g. `System.Collections.Immutable.dll`) on PATH or in the GAC, causing version conflicts at load time. Mitigation: the zip uses a flat directory (no subdirectory probing), so DLL loading picks up adjacent copies first. Document that the zip directory should be isolated.
- Question: should the zip also include `CustomBuildTasks.dll` so consumers can use the MSBuild task path too? Answer (now): no — the exe is the standalone entry point; the MSBuild task is for the existing `RunCodeGen.targets` flow which requires the full repo structure anyway. A separate `CustomBuildTasks` library artifact can be added later.
- Question: should the first release be `v0.1.0` (pre-1.0, signals early/experimental) or `v1.0.0` (stable)? Answer (now): `v1.0.0` — the exe has been verified end-to-end, passes the black-box harness, and the API is stable per Goal 2.2.
- Dependency: the GitHub Actions workflow requires the repo to have Actions enabled on GitHub. Verify this before pushing the workflow file.

## Completion Checklist

- [ ] `scripts\package-release.ps1` builds Release, stages, zips, and checksums correctly
- [ ] `.github\workflows\release.yml` triggers on tag push and publishes a GitHub Release with assets
- [ ] Local verification: extracted zip runs `T4CodeGen.exe -h` with exit 0
- [ ] Black-box harness passes against the release zip exe
- [ ] `curl` fetch example in README works against a published release
- [ ] DOX updates landed (plans index, buildguild note, README quick-install section)
- [ ] First release tag `v1.0.0` pushed and GitHub Release published

## Notes for the Implementing Agent

- The packaging script must call `msbuild CustomBuildTasks.csproj /p:Configuration=Debug` before building `T4CodeGen.csproj` in Release — the solution build order requires the library DLL to exist first (see `agents/buildguild.md` Gotchas).
- The zip should contain a single `T4CodeGen\` directory (not a flat dump of files) so extracting produces a clean folder. The flat layout inside that directory is: `T4CodeGen.exe` + all `*.dll` files from `bin\Release\`.
- Do not include `.pdb` files in the zip — they are debug symbols, not needed by consumers, and add bulk.
- The `gh release create` command in the workflow should use `--draft` first so the release can be inspected before publishing, OR publish directly with a tag-triggered workflow (the latter is simpler for an automated flow).
- Keep the script simple and linear — no fancy abstractions. A straightforward sequence of `msbuild`, `Copy-Item`, `Compress-Archive`, and `Get-FileHash` is the right shape.
