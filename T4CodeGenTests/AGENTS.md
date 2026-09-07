# T4CodeGenTests

## Purpose

Automated black-box test harness for the standalone `T4CodeGen.exe` CLI front-end. A self-contained console project (`T4CodeGenTests.exe`) that spawns the built exe against an isolated scratch workspace and verifies its shipped contract: exit codes (`0`/`1`/`2`), stdout log / stderr failure forwarding, response files, `|`/`;` list separators, byte-identical incremental regeneration (including the no-op skip), and per-template failure isolation. No NuGet, no test framework, no network — plain `msbuild` + one exe.

## Ownership

- Owned by this folder.
- Consumed by the root `test.bat` and by `agents/buildguild.md` as the automated CLI verification step. It black-boxes `T4CodeGen.exe` only — it never edits `T4CodeGen/` or `CustomBuildTasks/`, and if a case exposes a real bug the fix belongs in a separate `BUG_` plan.

## Local Contracts

- `T4CodeGenTests.csproj` — classic console app, .NET Framework v4.7.2, AnyCPU, `OutputType=Exe`, `System`/`System.Core` references only. No `Microsoft.Build.*`, no engine/Roslyn, no NuGet references — the harness talks to the exe only over the process boundary.
- `Program.cs` — the case battery over `System.Diagnostics.Process`. `RunExe` spawns `T4CodeGen\bin\Debug\T4CodeGen.exe` with `WorkingDirectory` = the scratch workspace root, redirected stdout/stderr, and a 60s `WaitForExit` kill timeout. Each case gets a fresh `Scratch` workspace (fixtures copied to a disposable temp dir, mtimes normalized to `now − 2 days`). Prints `PASS name` / `FAIL name (reason)` per case and exits `0` only if all pass.
- Cases: `FreshRegeneration` (exit 0 + generated files byte-identical to the checked-in test-bed baseline, txt compared modulo its `obj`-embedded paths), `NoOpRerun` (second run byte-identical, templates skipped), `DirtyInputRegenerates` (touched seed refreshes only its generated outputs), `BrokenTemplateIsolation` (exit 1, stderr names `Broken.tt`, healthy outputs still produced), `MissingNameExits2` / `UnknownFlagExits2` / `MissingValueExits2` (usage + error on stderr), `ZeroArgsExits2` (usage → **stdout**, exit 2), `ResponseFileMissingExits2` (`Response file not found:` on stderr, no usage), `HelpExitZero` / `HelpAliasesExitZero` (`-h`/`-help`/`-?`/`/?` and help-anywhere → stdout usage, exit 0), `ListSeparators` (`;` set === `|` set byte-for-byte), `ResponseFile`/`ResponseFileCommentsAndTrimming` (`@test.rsp` === explicit args byte-for-byte; the second also pins comment/blank-line/whitespace grammar), `CaseInsensitiveFlags`, `RepeatedFlagAppends` (accumulation + de-dup; the equivalence cases also assert successful runs write nothing to stderr).
- `Fixtures/` — the exe's input set, mirrored from the test bed so CWD-relative inputs and generated content match: `FancyWrite.h`, `FancyWrite.cpp`, `Main.cpp`, `T4Templates\HeaderExample.tt`, `T4Templates\TestTemplate.tt`, `T4Templates\CodeGenUtilities.ttinclude`, plus a deliberate `T4Templates\Broken.tt` (a compile-time error — `int broken = "not an int";` — so `ProcessTemplateInProcess` returns `didSucceed=false` deterministically; not a runtime exception).

## Work Guidance

- Keep the fixture seeds/templates byte-identical to the test-bed copies; change both sides in the same change (`FreshRegeneration` byte-compares the generated headers and normalized txt against the checked-in task baseline, so drift fails loudly).
- Debug-vs-Release: the harness resolves `T4CodeGen\bin\Debug\T4CodeGen.exe`; build the library and the exe in Debug before running tests (matches the Debug library path hardcoded by `RunCodeGen.targets`).
- Clock determinism is the harness's responsibility: baseline fixture mtimes are aged to `now − 2 days`, and cases that must observe a change stamp the touched file ≥2s after the last manifest write so the `>`/`>=` dirty comparisons cannot be floored by second-resolution timestamps.
- The harness never points `BaseIntermediateOutputPath`/`DefaultFileOutputPath` at the real test bed or the checked-in `*.t4generated.*` files; scratch directories are disposable.

## Verification

- `test.bat` (or `msbuild T4CodeGenTests\T4CodeGenTests.csproj` then run `bin\Debug\T4CodeGenTests.exe`) — offline; must print `PASS` for every case and exit `0`.
- Sabotage check: temporarily make `T4CodeGen\Program.cs` always return `2`, rebuild the exe, rerun the harness → it must exit non-zero with a named `FAIL`.

## Child DOX Index

None.