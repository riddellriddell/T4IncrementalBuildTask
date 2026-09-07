# Implementation Plan: Explicit Integration Contract for `T4CodeGen.exe`

version: 0.0
owner: "Your Name"
repo: "T4CodeGenLibrary"

---

## Metadata

- Task Type: `FEATURE`
- Task Name: Explicit integration contract for `T4CodeGen.exe` in `T4CodeGen/README.md`
- Status: `Draft`
- Owner: "Your Name"
- Last Updated: `2026-09-07`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [buildguild.md](../../buildguild.md)
- Milestone: [milestones.md](../milestones.md) (Milestone 2 complete; this is follow-on hardening of the CLI front-end)
- Goal: [goals2.md](../goals2.md) (Goal 2.2 CLI parity — acceptance was "works", not "specified")

## Objective

Turn `T4CodeGen/README.md` from a usage guide into a *normative integration contract* for `T4CodeGen.exe`, so that anyone — or anything, including the black-box harness — can consume the exe without reading `Program.cs`. The README must specify, precisely and unambiguously:

1. **Response-file grammar** — `@file` token, one argument per line, line trimming, `#` comment lines, blank-line skip, missing-file behavior.
2. **Argument semantics** — each of the six inputs (`-Name`, `-InputFiles`, `-T4Templates`, `-GeneratedFiles`, `-BaseIntermediateOutputPath`, `-DefaultFileOutputPath`), the `@response.rsp` token, and the help aliases; including the parser's actual quirks (case-insensitive flags, list separators and dedup, repeated-flag accumulation, `-GeneratedFiles` optional).
3. **Exit codes** — `0` / `1` / `2`, each meaning pinned and each reachable path enumerated (including zero-args and response-file-not-found).
4. **`T4Gen_*` output markers** — `T4Gen_TemplateFile(...)`, `T4Gen_InputFile(...)`, `T4Gen_Destination(...)`: exact token syntax, the regexes the compiler scans for, what each marker must contain, and the three template parameters templates must declare (`OutputFolder`, `ChangeFileManifest`, `GlobalFileManifest`).
5. **stdout vs stderr channel contract** — which channel carries compiler logs, which carries parse errors / usage-on-error / failure text.

The contract must match the actual behavior of `Program.cs` + `TemplateCompiler.cs` exactly. No behavior change to the exe or compiler is in scope; where README and code disagree today, the README is corrected to the code, and any wording the code should instead be corrected to match is flagged.

## Problem Summary

- `T4CodeGen/README.md` is a usage walkthrough ("How do I run it?"), not a spec. It states exit codes `0`/`1`/`2` and a one-line response-file rule, but leaves the precise grammar unwritten: what counts as an argument for `@file` (per-line, trimmed, comments), which help aliases exist (it omits `/?` and that a help token anywhere triggers help), whether flags are case-sensitive (they are not), what happens when a response file is missing (stderr + exit 2, no usage), what zero-args does (usage to **stdout** + exit 2), that `-GeneratedFiles` is optional, and how lists are split/deduped (pipe wins if mixed, entries trimmed and de-duplicated).
- Downstream consumers need an unambiguous spec: the `T4CodeGenTests` harness asserts behavior case by case, CI/scripts consume the exe over the process boundary, and the "tool you consume" framing requires the contract to be discoverable from the README alone.
- The `T4Gen_*` markers and the three template parameters are documented only in `CustomBuildTasks/AGENTS.md` and `T4IntegrationTestBed/T4Templates/AGENTS.md` (template conventions) and implemented in `TemplateCompiler.cs` (scan regexes). A consumer authoring a template or a response file today must read source to learn the exact tokens.
- Intent of the change (from the task): the exe stops being "a source you fork" and becomes "a tool you consume", and the tests get a spec to assert against.

## Scope

- In scope: writing the precise, normative contract into `T4CodeGen/README.md` (response-file grammar; argument semantics for all six inputs + `@` + help aliases incl. `/?` and any-argument help detection; list separator rule with `|`-precedence, trim and dedup; flag case-insensitivity; repeated-flag accumulation; `-GeneratedFiles` optional; exit code `0`/`1`/`2` with all reachable paths; stdout vs stderr channel contract; `T4Gen_*` marker contract with the exact scan regexes and the three template parameters).
- In scope: a "test coverage" section or mapping in the README (or `T4CodeGenTests/AGENTS.md`) that names which documented statements the harness already asserts, and which documented statements are not yet pinned.
- In scope: correcting README wording wherever it currently contradicts `Program.cs` behavior (e.g. add `/?`, document zero-args and missing-response-file behavior).
- In scope: optionally adding black-box cases for documented-but-unasserted contract details (`ResponseFileMissingExits2`, `ZeroArgsExits2`, help aliases, comment/blank-line rsp lines, case-insensitive flags, missing value, repeated flags) — only if the implementing agent confirms they pin real contract wording introduced by this plan.
- In scope: DOX pass — reflect any README wording changes in `T4CodeGen/AGENTS.md`; add new cases to `T4CodeGenTests/AGENTS.md` case list if cases are added; register this plan in `agents/plans/AGENTS.md`.
- In scope: optional 1-line help-text wording fix in `T4CodeGen/Program.cs:78` ("one per line or space separated" → "one argument per line"), text-only, no behavior change — only if the plan author approves it (it is technically a code file edit).
- Out of scope: any behavior change to `T4CodeGen/Program.cs` or `CustomBuildTasks/TemplateCompiler.cs` (arg grammar, exit codes, marker regexes, failure text). If a test added by this work exposes a real contract violation, the fix belongs in a separate `BUG_` plan.
- Out of scope: redesigning the arg grammar (the grammar is documented as-is; both list separators remain).
- Out of scope: the tool relocation / self-contained-project changes (`FEATURE_self-contained-t4codegen-project.md`) and wildcard expansion (`FEATURE_wildcard-path-support.md`) — separate plans, this contract is orthogonal.
- Out of scope: the MSBuild task (`BuildT4TextFiles`) contract, `RunCodeGen.targets`, and `AddMatchingFilesToOutput.cs`.

## Current State

### The exe contract as implemented (`T4CodeGen/Program.cs`, 220 lines)

- **Expansion first:** `ExpandResponseFiles` walks the raw args; any arg `@<path>` (length > 1) with a readable file is replaced by its lines — each line `Trim()`ed, blank lines skipped, lines starting with `#` skipped, each remaining line becomes one argv entry verbatim (no quote stripping, no intra-line splitting). A missing/readable-fail `@path` writes `Response file not found: <path>` to **stderr** and exits `2` immediately (`Environment.Exit`, no usage). Non-`@` args pass through.
- **Help:** if the expanded args are empty **or** any arg ordinal-equals `-h`, `-help`, `-?`, or `/?` (`Program.cs:55-58`), usage is printed to **stdout**. Exit `0` when a help token was present; exit `2` when args were empty. Help wins over everything (checked before parse).
- **Parse** (`TryParse`, `Program.cs:130-199`): iterates `flag`, then `value = next arg` (a trailing bare flag is a `Missing value for argument: <flag>` error). Flags are matched **case-insensitively** (`flag.ToLowerInvariant()`) against `-name`, `-inputfiles`, `-t4templates`, `-generatedfiles`, `-baseintermediateoutputpath`, `-defaultfileoutputpath`. Anything else → `Unknown argument: <flag>` + usage on **stderr** + exit `2`. Scalar values last-write-wins; list values **accumulate** across repeated occurrences.
- **Lists** (`AppendList`, `Program.cs:201-218`): if the value contains `|`, split on `|`; otherwise split on `;`; `RemoveEmptyEntries`; each part trimmed; de-duplicated (`Contains`, case-sensitive). Mixed-separator values split on `|` only. A repeated list flag appends (deduped).
- **Required args:** `-Name`, `-InputFiles` (≥1), `-T4Templates` (≥1), `-BaseIntermediateOutputPath`, `-DefaultFileOutputPath`. `-GeneratedFiles` is **not** required. Missing → `Missing required argument: <flag>` + usage on **stderr** + exit `2`.
- **Run:** `TemplateCompiler.Compile(name, inputs, templates, generated, baseIntermediate, defaultOutput, log)` with `log = line => Console.WriteLine(line)` — the whole compiler log stream goes to **stdout**. All `TemplateFailures` are forwarded verbatim to **stderr** (`Console.Error.WriteLine`). Exit is `result.Success ? 0 : 1`.

### Compiler side (`CustomBuildTasks/TemplateCompiler.cs`)

- `Compile` (`:25-33`) concatenates `baseIntermediateOutputPath + "GlobalFileManifest.T4Manifest"` for the last-build-time state (`:39`), and `baseIntermediateOutputPath + "GeneratedFiles"` for the temp output folder (`:283`). These two path formulas are part of the executable contract a consumer must understand to author a response file.
- **Markers** are scanned from generated files by regex, group 1 captured (`:167`, `:207`, `:413`): `T4Gen_TemplateFile\((.*?)\)`, `T4Gen_InputFile\((.*?)\)`, `T4Gen_Destination\((.*?)\)`. In practice templates embed them as `//T4Gen_<name>(<path>)` comments; the scan is regex-based so any comment text works. They drive invalidation (template/input changed-or-deleted since last build → regenerated/deleted) and the destination override.
- **Three template parameters** passed via `AddParameter(null, null, <name>, <value>)` (`:513-515`): `OutputFolder` (temp `GeneratedFiles` folder), `GlobalFileManifest` (manifest abs path), `ChangeFileManifest` (the per-template `<TemplateName>.T4ChangedManifest` next to the `.tt`, one dirty file path per line, `:326-342`).
- Per-template failure text format: `T4 Template <templateFilePath> failed: <error text>` (`:392-393`), one entry per failing template; the exe forwards these lines to stderr verbatim → exit `1`. All templates are still attempted; `Success=false` iff any failed.

### Test bed templates (`T4IntegrationTestBed/T4Templates/`)

- Both `HeaderExample.tt` and `TestTemplate.tt` declare `<#@ parameter type="System.String" #>` for `OutputFolder`, `ChangeFileManifest`, `GlobalFileManifest` and embed the three markers; `HeaderExample.tt` emits `T4Gen_Destination(<dir of header>)` so its output is copied back beside the source header (`HeaderExample.tt:53-59,69`). `CodeGenUtilities.ttinclude` provides `FlushCurrentContextToFile` etc. Templates also use the author marker `T4Gen_RUN_TEXT_TEMPLATE_ON_THIS(<name>)` inside seed `.h` files (`HeaderExample.tt:28`) — a template-input convention, not an exe-channel contract, but relevant to the marker family.

### README today (`T4CodeGen/README.md`, 69 lines)

Covers build, a usage line, an args table (`-Name`… `-DefaultFileOutputPath`, `@response.rsp`, `-h`/`-help`/`-?`), "Lists are pipe (`|`) or semicolon (`;`) separated", working-directory guidance, exit codes `0`/`1`/`2`, a worked test-bed example, and notes that the CLI is a thin mapper and per-template failures match the task's error text.

**README-vs-code gaps the plan must resolve (all wording-only):**
1. Help aliases: README omits `/?` and the "help token anywhere wins" rule (`Program.cs:57`).
2. Zero-args behavior undocumented: usage → **stdout**, exit `2`.
3. Response-file-not-found undocumented: `Response file not found: <path>` → stderr, exit `2`, no usage.
4. Flag case-insensitivity undocumented.
5. Response-file grammar under-specified: trimming, blank-line skip, one-arg-per-line (no space splitting), no quote stripping — README already says "one argument per line; `#` starts a comment", which matches code, but the *exe's own help text* (`Program.cs:78`) claims "one per line or space separated", which is wrong and should either be corrected in code (help-text-only) or deliberately contradicted in README.
6. List semantics under-specified: `|` precedence on mixed-separator values, trimming, dedup, repeated-flag accumulation, `-GeneratedFiles` optional.
7. Marker syntax documented only in prose ("`T4Gen_TemplateFile(...)`" etc.); the exact scan regex and the "group 1 path, any comment form" mechanics, plus the three parameters and their path formulas, are not in the README.

### Harness assertions today (`T4CodeGenTests/Program.cs` + `AGENTS.md`)

Existing cases: `FreshRegeneration` (exit 0, byte-parity of `*.t4generated.h` vs checked-in test-bed baseline, txt modulo `obj` path lines), `NoOpRerun` (exit 0, stdout contains `has no dirty files`, bytes unchanged), `DirtyInputRegenerates`, `BrokenTemplateIsolation` (exit 1, stderr names `Broken.tt`, healthy outputs still produced, no partial temp leftovers), `MissingNameExits2` (exit 2, stderr `Missing required argument: -Name` + `Usage`), `UnknownFlagExits2` (exit 2, stderr `Unknown argument: -bogus` + `Usage`), `ListSeparators` (`;` ≡ `|` byte-for-byte), `ResponseFile` (`@test.rsp` ≡ explicit args byte-for-byte), `HelpExitZero` (`-h` → stdout `Usage`, exit 0).

**Harness gaps vs the new contract:** response-file-not-found exit 2 + stderr text; comment/blank/trimming rsp lines (the rsp fixture has none); `-help`/`-?`/`/?` aliases and help-anywhere; zero-args → exit 2 with usage on **stdout**; missing-value-for-flag error text; case-insensitive flags; repeated-flag accumulation; `-GeneratedFiles` optional. These are candidates for new cases only where the plan's README wording makes them normative.

## Assumptions and Constraints

- `T4CodeGen/README.md` is the living, normative spec; `T4CodeGen/Program.cs` is the reference implementation the spec must match. Where the README is ambiguous today, the plan writes a definitive wording that matches observed behavior; never the other way around.
- No behavior change to `Program.cs` or `TemplateCompiler.cs`. The only permitted code-file edit is the `PrintUsage` help-text wording at `Program.cs:78` (one per line) — text-only; approve explicitly before doing it.
- Both list separators (`|` and `;`) are documented as permanent, with `|` taking precedence when a single value mixes them (that is what `AppendList` does).
- Marker and parameter documentation must stay consistent with `CustomBuildTasks/AGENTS.md` and `T4IntegrationTestBed/T4Templates/AGENTS.md`; the templates and the compiler regexes are the source of truth, not the other way around.
- The contract is additive: nothing currently asserted by `T4CodeGenTests` may stop matching after the README rewrite.
- Offline/zero-NuGet constraints hold; verification stays `test.bat` + `msbuild`.

## Files and Areas Likely Affected

- `T4CodeGen/README.md` — the primary deliverable: rewritten into the five contract sections (response-file grammar, argument semantics, exit codes, `T4Gen_*` markers + template parameters, stdout/stderr channels) plus a test-coverage mapping and the retained build/usage/example content.
- `T4CodeGen/Program.cs` — only if approved: `PrintUsage:78` "one per line or space separated" → "one argument per line". Text-only.
- `T4CodeGenTests/Program.cs` — optional new cases (`ResponseFileMissingExits2`, `ZeroArgsExits2`, `HelpAliasesExitZero`, `ResponseFileCommentsAndTrimming`, `CaseInsensitiveFlags`, `MissingValueExits2`, `RepeatedFlagAppends`) — only for contract details the README makes normative and the harness does not yet pin.
- `T4CodeGenTests/AGENTS.md` — case-list update if new cases land.
- `T4CodeGen/AGENTS.md` — verify the Local Contracts wording still matches the README contract after the rewrite (especially list/response-file grammar and exit-code details).
- `agents/plans/AGENTS.md` — register this plan in the `implementation plans/` index.
- This plan file: `agents/plans/implementation plans/FEATURE_exe-integration-contract.md`.

## Implementation Steps

1. **Re-pin the source of truth.** Re-read `T4CodeGen/Program.cs` (`ExpandResponseFiles`, `HasHelp`, `TryParse`, `AppendList`, exit plumbing) and the marker/parameter code in `CustomBuildTasks/TemplateCompiler.cs` (`:39`, `:167`, `:207`, `:283`, `:326-342`, `:392-393`, `:410`, `:513-515`); produce a reference table of grammar rules and the two path formulas (`<BaseIntermediateOutputPath>GlobalFileManifest.T4Manifest`, `<BaseIntermediateOutputPath>GeneratedFiles`). Confirm the README-vs-code gap list in Current State items 1-7.
2. **Draft the contract sections** in `T4CodeGen/README.md`:
   - *Response files:* `@file` expands to its lines after `Trim()`; blank and `#`-prefixed lines are ignored; each remaining line is one argument (no quotes stripped, no space-splitting — quote nothing, each flag/value on its own line); a missing response file writes `Response file not found: <path>` to stderr and exits `2` without printing usage. `@` args can be mixed with CLI args (CLI args first, then response-file args, in order).
   - *Argument semantics:* a table for the six inputs (with required/optional, list or scalar, accumulate-vs-last-wins, and the exact path formulas for `-BaseIntermediateOutputPath`), plus `@file`, plus the help aliases `-h`/`-help`/`-?`/`/?` — any one appearing anywhere causes help to stdout and exit `0`. Flags are case-insensitive. `-GeneratedFiles` is optional (invalidation input only). Lists split on `|` if present, else `;`; entries trimmed and de-duplicated; repeated list flags append.
   - *Exit codes:* `0` success; `1` at least one template failed (failure lines on stderr, healthy templates still produced); `2` invalid use — enumerate the five reachable paths: zero args (usage → **stdout**), missing response file (`Response file not found:` → stderr, no usage), unknown flag (error + usage → stderr), missing value (error + usage → stderr), missing required argument (error + usage → stderr).
   - *stdout vs stderr:* stdout is the compiler log channel (all `TemplateCompiler.Compile` log lines plus in-process template output); stderr is error/failure channels — parse errors, usage-on-error, missing-response-file, and the forwarded per-template failure lines. Happy-path success writes nothing to stderr.
   - *Generated-file / marker contract:* every generated file should embed `//T4Gen_TemplateFile(<template path>)` and `//T4Gen_InputFile(<input path>)` markers; optional `//T4Gen_Destination(<folder>)` overrides the copy destination (default: `DefaultFileOutputPath` with the `<BaseIntermediateOutputPath>GeneratedFiles` prefix replaced). Give the exact scan regexes (group 1 = path). Document the three template parameters templates must declare — `OutputFolder`, `ChangeFileManifest`, `GlobalFileManifest` — with their path formulas and per-template `*.T4ChangedManifest` semantics. Note `T4Gen_RUN_TEXT_TEMPLATE_ON_THIS(<name>)` as the template-authors' seed-file tag convention (not an exe-channel contract).
   - *Test coverage mapping:* which statements the harness asserts today (per `T4CodeGenTests/AGENTS.md`) and which are not yet pinned.
3. **Cross-check every sentence against the harness.** Walk `T4CodeGenTests/Program.cs` cases; confirm no documented statement contradicts an existing assertion; resolve each Current-State gap with definitive wording; note in the README (or `T4CodeGenTests/AGENTS.md`) the documented-but-unasserted items.
4. **Optional wording fix:** if approved, change `Program.cs:78` to `(@response file: one argument per line; '#' starts a comment)` and rebuild. Not a behavior change; keep it out if it risks scope creep.
5. **Optional new black-box cases** (only where README wording is normative and unasserted): `ResponseFileMissingExits2`, `ResponseFileCommentsAndTrimming`, `ZeroArgsExits2`, `HelpAliasesExitZero`, `CaseInsensitiveFlags`, `MissingValueExits2`, `RepeatedFlagAppends`. Update the `T4CodeGenTests/AGENTS.md` case list.
6. **DOX pass.** Update `T4CodeGen/AGENTS.md` (Local Contracts wording must match the new README contract), `T4CodeGenTests/AGENTS.md` if cases changed, and register this plan in `agents/plans/AGENTS.md`.

## Verification Plan

### Automated Checks

- `test.bat` (or its three `msbuild` steps + `T4CodeGenTests\bin\Debug\T4CodeGenTests.exe`) — offline; every case prints `PASS` and the harness exits `0`. This is the exe contract's executable check: all nine existing cases must still pass after the README rewrite, and any new cases must pass too.
- `msbuild T4CodeGen\T4CodeGen.csproj` (and the optional help-text wording change if approved) — builds clean; `-h` still prints usage with the corrected response-file line.
- Grep-check the README's normative statements against source: every grammar/exit-code/channel/marker claim in the README should have a `Program.cs`/`TemplateCompiler.cs` line reference in this plan's review notes.

### Manual Checks

1. **Reader acceptance:** a person (or fresh agent) who has read only `T4CodeGen/README.md` must be able to author a working response file (flags and values on separate lines, comments allowed) and correct exit-code handling (`0` rerun-safe success, `1` failed template named on stderr, `2` usage error) without opening `Program.cs`.
2. **Behavior spot-check:** from the test bed dir, run `T4CodeGen.exe -h`, `T4CodeGen.exe` (zero args), and `T4CodeGen.exe @missing.rsp`, and confirm the stdout/stderr + exit codes match the README table exactly.
3. **Marker spot-check:** confirm the regenerated `*.t4generated.h` embed `T4Gen_TemplateFile`/`T4Gen_InputFile`/`T4Gen_Destination` lines matching the README's notation, and that `T4Templates/AGENTS.md`'s parameter/marker text agrees with the README's.

## Risks and Open Questions

- Risk: **both list separators forever.** The README will promise `|` and `;` with `|` precedence on mixed values. Dropping either later becomes a contract break; the harness `ListSeparators` case pins the promise. Confirm the intent to keep both permanently before finalizing this wording.
- Risk: **stderr text stabilization.** The failure line (`T4 Template <path> failed: …`) originates in `TemplateCompiler.cs:392-393` and its tail is engine error text. The README can promise "one line per failing template naming the template" but must not promise byte-stable error bodies; keep the wording at that level.
- Risk: **`T4Gen_Destination` quirk.** `TemplateCompiler.cs:418-435` applies the destination override under `if (!didSucceed)` (i.e., when the scan *failed*), which reads inverted versus the intent documented in `CustomBuildTasks/AGENTS.md`. The fixture layout (seeds at workspace root) makes this unobservable in tests, so the README's destination contract is intent, not verified behavior. Do not claim more than: "an optional `T4Gen_Destination(<folder>)` marker designates the copy-back folder; default is `DefaultFileOutputPath` with the `GeneratedFiles` prefix replaced" — and flag this as a candidate `BUG_` investigation, out of scope here.
- Risk: **help-text edit is a code edit.** Changing `Program.cs:78` is the one code-file touch in this plan; if reviewers prefer a zero-code plan, the README simply documents one-argument-per-line and deliberately doesn't quote the mismatch. Decide in step 4.
- Question: should newly-documented-but-unasserted details get new harness cases in this plan, or a follow-up `FEATURE_exe-test-contract-coverage.md`? Recommend: add cases only for details the README makes normative and that are cheap to assert (exit 2 + message text); defer exotic ones.
- Question: does the README contract belong entirely in README, or should a `T4CodeGen/CONTRACT.md` (referenced from README) hold the normative text? Recommendation: README only — one living spec, matches the task.
- Dependency: byte-parity cases require the fixture set to keep matching the test bed (`T4CodeGenTests/AGENTS.md`); no fixture changes are needed for this plan.

## Completion Checklist

- [ ] Implementation matches the linked design and goal context
- [ ] Scope stayed within this plan
- [ ] Verification steps were completed or explicitly deferred
- [ ] Relevant status docs were updated
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Read `agents/buildguild.md` first; the canonical flow is library → exe → tests → test bed, and `test.bat` is the one-command entry point. Run it before and after the README change to prove zero behavioral drift.
- Do not invent behavior. Every normative sentence must trace to a line in `Program.cs`, `TemplateCompiler.cs`, or the test templates. The Current State section above already pins the grammar and markers — validate them against the files before quoting numbers/examples in the README.
- Keep `T4CodeGen/README.md`'s existing worked test-bed example and the "thin mapper / do not re-implement pipeline logic" notes; fold the contract in as the normative top half and keep usage as the worked half, or renumber sections so the contract reads first.
- The five exit-2 paths (zero args, missing response file, unknown flag, missing value, missing required argument) each have a different stdout/stderr composition — spell each out in the table; this is the highest-value part of the contract for consumers.
- `TestTemplate.t4generated.txt` embeds absolute `obj` paths ("Output folder:" / "Global Manifest :" lines) that the harness normalizes (`NormalizeTxt`) before comparing — do not use that txt as a marker/path example in the README; use the headers.
- If new harness cases are added, keep them byte-parity-free (they only assert stdout/stderr text + exit codes) so they stay deterministic and cheap.
- Write the README's marker examples exactly as the templates emit them: `//T4Gen_TemplateFile(<path>)` with the leading `//`, since consumers grepping generated files will match that literal prefix.