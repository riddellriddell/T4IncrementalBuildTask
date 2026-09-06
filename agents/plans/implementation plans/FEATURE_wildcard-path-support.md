# Implementation Plan: Wildcard Support in Path Lists

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `FEATURE`
- Task Name: Wildcard (`*`/`?`/`**`/`[]`) expansion in the three path lists
- Status: `Draft` (2026-09-05)
- Owner: "Your Name"
- Last Updated: `2026-09-05`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [buildguild.md](../../buildguild.md)
- Milestone: [milestones.md](../milestones.md) (Milestones 1-2 complete; this is follow-on hardening of the standalone pipeline)
- Goal: [goals2.md](../goals2.md) (Goal 2.2 CLI parity, not wildcard-covered)

## Objective

Ship host-agnostic wildcard expansion for the pipeline's path lists (`InputFiles`, `T4Templates`, `GeneratedFiles`) so the CLI front-end (`T4CodeGen.exe`) accepts MSBuild-style glob patterns and expands them identically to how MSBuild pre-expands items for the task — with the task path untouched and byte-identical before/after. Includes declarative wildcard pickup for templates in the test bed and black-box test cases guarding the new contract.

## Problem Summary

- The MSBuild task path already globs: MSBuild expands item `Include` wildcards before `BuildT4TextFiles` runs (`RunCodeGen.targets:28` globs `$(MSBuildProjectDirectory)\**\*.t4generated.*`), so the core receives concrete file lists.
- The CLI path does not: `T4CodeGen/Program.cs` maps `-InputFiles "*.h"` straight through to `TemplateCompiler.Compile`, which treats the literal pattern as a file path. `File.GetLastWriteTime("*.h")` returns 1601-01-01 (no exception), so the entry is silently never-dirty (`TemplateCompiler.cs:85`); a template glob is silently skipped forever. There is no error and no way to discover the mistyped pattern.
- The test bed declares each template explicitly (`T4IntegrationTestBed.vcxproj:141`), so a new `.tt` file in `T4Templates/` is not picked up until the project file is edited — unlike the native MSBuild globs already used elsewhere.
- A subtle footgun must be designed against: relative `GeneratedFiles` entries never match the absolute `destinationFilePath` the copy step computes (`TemplateCompiler.cs:438`), and the invalid-file cleanup then deletes regenerated outputs. Absolute generated paths are already required today; the wildcard rule must preserve that.

## Scope

- In scope: new host-agnostic `CustomBuildTasks/PathExpander.cs` (no `Microsoft.Build.*`, no NuGet) implementing MSBuild-compatible glob expansion.
- In scope: wiring expansion of the three path lists at the top of `TemplateCompiler.Compile`, with pass-through/no-op behavior for already-expanded literal lists (the task's input) and the `GeneratedFiles` full-path normalization rule.
- In scope: declarative template pickup in `T4IntegrationTestBed.vcxproj` via `<TextTemplateFile Include="T4Templates\*.tt" />`.
- In scope: `-h` usage text + `T4CodeGen/README.md` wildcard documentation.
- In scope: new `T4CodeGenTests` black-box cases covering glob-vs-explicit equivalence, relative `GeneratedFiles` glob safety, and zero-match behavior.
- In scope: DOX updates (`agents/plans/AGENTS.md` index, `CustomBuildTasks/AGENTS.md`, `T4CodeGen/AGENTS.md`, `T4CodeGenTests/AGENTS.md`, `agents/buildguild.md` if its verify flow mentions the CLI contract).
- Out of scope: wildcards in `BaseIntermediateOutputPath`/`DefaultFileOutputPath` (single folders, pattern is meaningless).
- Out of scope: new task/MSBuild parameters; changes to list separators, response files, or `RunCodeGen.targets` item construction.
- Out of scope: `AddMatchingFilesToOutput.cs` (dead prototype) and the legacy `T4IntegrationTestBed\RunCodeGen.targets`/`.xml`.
- Out of scope (phase 1): `%HH` escaping for literal `*?[]` filenames — flagged in Risks/Open Questions; the matcher treats any entry containing unescaped glob chars as a pattern.

## Current State

- `TemplateCompiler.Compile` (namespace `T4BuildTools`) is the standalone, MSBuild-independent pipeline; it treats every entry of `inputFiles`/`t4Templates`/`generatedFiles` as a concrete path and calls `File.GetLastWriteTime`/`File.Exists` directly. A missing literal path silently yields the 1601 default stamp (never dirty); a missing template is silently skipped. `Program.cs` is a thin argument-to-API mapper per its contract and must not re-implement pipeline logic.
- The generated-file markers (`T4Gen_TemplateFile`/`T4Gen_InputFile`) embed the path strings exactly as passed, so expansion must preserve the task's path form (relative inputs, absolute generated files) or CLI output diverges and the checked-in byte-parity tests fail.
- The black-box harness (`T4CodeGenTests/Program.cs`) currently passes only explicit paths and has an existing list of `PASS`/`FAIL` cases; the scratch workspace mirrors the test bed layout (seeds at root, templates under `T4Templates\`, deliberate `Broken.tt` at `T4Templates\Broken.tt`).

## Assumptions and Constraints

- .NET Framework 4.7.2 with `System`/`System.Core` only — no `Microsoft.Extensions.FileSystemGlobbing`, no network, no new vendored assemblies. The glob matcher is hand-rolled.
- Task input parity is a hard guarantee: the task's already-expanded literal lists must round-trip through the expander unchanged (identity for wildcard-free entries), so task-produced generated bytes are identical before and after this feature.
- Path-form parity: relative pattern → relative matches, absolute pattern → absolute matches; matches keep CWD-relative spelling (no `.\` prefix) so `T4Gen_*` markers and `.T4ChangedManifest` output are byte-identical to the task's relative items.
- `GeneratedFiles` matches resolve via `Path.GetFullPath` against CWD (the task's absolute-glob equivalent), preventing the relative-vs-absolute delete footgun.
- Deterministic expansion: matches are deduped (ordinal-ignore-case) and ordinally sorted; explicit (wildcard-free) entries keep their original list position.
- Only files are returned — `Directory.GetFiles` surfaces, not directories.
- Zero-match rules: `T4Templates` pattern that matches nothing is a build failure (a listed template resolving to nothing is a config error); `InputFiles` zero-match logs a warning and continues (a seed may legitimately be absent); `GeneratedFiles` zero-match logs info (fresh build).
- No regression to deletion semantics: a literal (wildcard-free) missing input/template keeps today's silent never-dirty behavior (changing that is a separate BUG_ plan).
- Matcher costs one tree enumeration per expanded pattern (under the pattern's static prefix, recursive); acceptable for codegen-scale trees, note in reviewer expectations.

## Files and Areas Likely Affected

- `CustomBuildTasks/PathExpander.cs` — new: static `ExpandList(IList<string>, bool normalizeGeneratedToFullPath, Action<string> log, List<string> errors)` + the private pattern→regex/segment matcher; no `Microsoft.Build.*`.
- `CustomBuildTasks/TemplateCompiler.cs` — expand the three lists at entry (after the initial folder/state logs, before the dirty scan), feed `errors` for `T4Templates` into `result.TemplateFailures` and `Success`.
- `T4IntegrationTestBed.vcxproj` — replace the two explicit `TextTemplateFile` items with `<TextTemplateFile Include="T4Templates\*.tt" />`.
- `T4CodeGen/Program.cs` — `PrintUsage` only: document `*`/`?`/`**`/`[]` for the three list params.
- `T4CodeGen/README.md` — wildcard section with the worked CLI example (`-InputFiles "*.h|*.cpp"`, `-T4Templates "T4Templates\*.tt"`, `-GeneratedFiles "*.t4generated.*"`).
- `T4CodeGenTests/Program.cs` — new cases `InputGlobEquivalent`, `TemplateGlobEquivalent`, `GeneratedGlobRelative`, `ZeroMatchTemplateFails`, `ZeroMatchInputWarns`.
- Docs/DOX: `agents/plans/AGENTS.md` (index line), `CustomBuildTasks/AGENTS.md`, `T4CodeGen/AGENTS.md`, `T4CodeGenTests/AGENTS.md`, `agents/plans/implementation plans/FEATURE_wildcard-path-support.md` (this file).

## Implementation Steps

1. Add `CustomBuildTasks/PathExpander.cs`:
   - Detect patterns: an entry is a pattern iff it contains an unescaped `*`, `?`, `[`, or `]`; otherwise returned verbatim.
   - Dialect: `*` (zero+ chars within a segment), `?` (one char within a segment), `**` (zero+ directories, only as a segment), `[...]`/`[!...]` char classes; MSBuild-compatible subset.
   - Mechanics: walk the pattern left-to-right up to the first wildcard char to get a static prefix; enumerate files via `Directory.GetFiles`/`GetDirectories` (recursive) under that prefix; filter candidates with a per-segment regex derived from the pattern; emit only files.
   - Normalize spellings: relative pattern → relative match, absolute → absolute; `GeneratedFiles` callers pass `normalizeToFullPath=true`.
   - Dedupe (ordinal-ignore-case) + ordinal sort of the wildcard-produced group; keep explicit entries' positions.
   - Zero-match → `errors`/`log` messaging handled by the caller; the expander itself only reports match counts.
2. Wire into `TemplateCompiler.Compile`: after the manifest/state read and the initial `log` block, expand the three lists once (templates and inputs path-form-preserved, generated normalized to full path); log each pattern's match count; forward `T4Templates` zero-match entries into `TemplateFailures` and clear out the empty matches; keep the rest of the pipeline unchanged.
3. Update `T4IntegrationTestBed.vcxproj` to `<TextTemplateFile Include="T4Templates\*.tt" />`; verify the two existing templates still feed `GenerateT4Files` (MSBuild-native pickup of the new glob).
4. Add the `T4CodeGenTests` cases (each byte-compares against an explicit-list baseline run in the same scratch):
   - `InputGlobEquivalent` — `-InputFiles "*.h|*.cpp"` (two patterns) vs the three explicit seed names: same exit 0 + byte-identical generated outputs.
   - `TemplateGlobEquivalent` — `-T4Templates "T4Templates\*.tt"` (matches `HeaderExample.tt`, `TestTemplate.tt`, and `Broken.tt` in the fixture set) vs the explicit 4-item list: same exit code (1 — `Broken.tt` fails) and same generated bytes.
   - `GeneratedGlobRelative` — `-GeneratedFiles "*.t4generated.*"` relative (must be normalized to full path internally): exit 0 and outputs present — guards the delete footgun; second run stays a clean no-op.
   - `ZeroMatchTemplateFails` — `-T4Templates "T4Templates\nope\*.tt"` → exit 1, stderr names the pattern.
   - `ZeroMatchInputWarns` — `-InputFiles "nope\*.h"` → exit 0, stdout mentions the zero-match pattern.
5. Update `-h` usage text and `T4CodeGen/README.md` with the wildcard syntax and the relative-`GeneratedFiles`-resolves-absolute note.
6. DOX pass: register the plan under `agents/plans/AGENTS.md`; update the three child AGENTS.md docs and `agents/buildguild.md` where the CLI contract or fixture set is described.

## Verification Plan

### Automated Checks

- `test.bat` (or the three `msbuild` steps + `T4CodeGenTests\bin\Debug\T4CodeGenTests.exe`) — offline; all cases including the five new ones print `PASS`; harness exits `0`.
- `msbuild T4IntegrationTestBed.sln` (Debug|x64) — task path unregressed; `GenerateT4Files` picks up the two templates via the vcxproj glob; generated headers byte-identical to the checked-in baseline.
- Byte-parity guard: `FreshRegeneration` (existing) still passes, proving the expander is identity for the explicit lists the task-style fixtures use.

### Manual Checks

1. Run `T4CodeGen.exe` from the test bed dir with `-InputFiles "*.h|*.cpp"` and task-equivalent generated paths; hash-compare the regenerated `*.t4generated.*` against a task-produced set (existing buildguild byte-identity check, now through a glob).
2. Add a throwaway `T4Templates\Probe.tt` in a scratch copy, confirm `T4Templates\*.tt` picks it up, then remove it (do not copy throwaway files into the real test bed).

## Risks and Open Questions

- Risk: CLI-vs-task divergence on `GeneratedFiles` — mitigated by the full-path normalization rule and pinned by `GeneratedGlobRelative`.
- Risk: `Directory.EnumerateFiles` recursion under a `**`-heavy pattern is unbounded (e.g. `**\*`) — acceptable at codegen scale, but the matcher should short-circuit non-recursive patterns to a single-segment `Directory.GetFiles`; note in reviewer expectations.
- Question: zero-match `T4Templates` as a hard failure changes today's silent skip — confirmed in the design as the correct, loud contract; revisit if the test bed relies on the old quiet behavior.
- Question: `%HH` escaping (`%2A`/`%3F`/`%5B`/`%5D`) for literal `*?[]` filenames — deferred to a follow-up; document in README that an unescaped glob char makes an entry a pattern.
- Open edge: char-class negation uses `[!...]` only (MSBuild-style); `[^...]` support optional and untested — do not claim parity beyond what's tested.
- Dependency: byte-parity cases require the fixture set to keep `Broken.tt` present (it makes `TemplateGlobEquivalent` deterministic); keep fixtures in sync per `T4CodeGenTests/AGENTS.md`.

## Completion Checklist

- [ ] Implementation matches the linked design and goal context
- [ ] Scope stayed within this plan
- [ ] Verification steps were completed or explicitly deferred
- [ ] Relevant status docs were updated
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Read `agents/buildguild.md` first; the canonical flow is library → exe → tests → test bed. `test.bat` is the one-command entry point.
- Keep `TemplateCompiler.cs` free of `Microsoft.Build.*`; `PathExpander` must be host-agnostic and take the `Action<string>` log sink (do not `Console.WriteLine` inside the library, matching `FileScanUtility`'s existing contract is not required — the expander should flow diagnostics through the sink).
- The task path must be a provable no-op: wildcard-free entries pass through verbatim, preserving order and spelling. Any change to task-side generated bytes fails `FreshRegeneration` loudly.
- Preserve the relative path spelling of matches exactly as the fixtures expect (no `.\`, no drive-letter prefix for relative patterns) or the marker/macro byte-parity checks collapse.
- Implement the `GeneratedFiles` normalization before the copy/invalidate stage runs; the harness comment at `T4CodeGenTests/Program.cs` about absolute generated paths is the exact failure mode this rule prevents.
- Do not touch directory-returning semantics, `BaseIntermediateOutputPath`/`DefaultFileOutputPath` handling, or the `Broken.tt` fixture when adding the template-glob case.