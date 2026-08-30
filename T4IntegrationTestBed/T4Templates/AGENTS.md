# T4Templates

## Purpose

T4 text templates and shared helper code that produce the test bed's `*.t4generated.*` outputs.

## Ownership

- Owned by this folder.
- Executed by `T4BuildTools.BuildT4TextFiles` via the root `..\..\RunCodeGen.targets`; templates do not run standalone in the normal build flow.

## Local Contracts

- `CodeGenUtilities.ttinclude` — shared template helpers: `FlushCurrentContextToFile` (writes the current generation context to a file and clears the buffer), `GetFileLinesAsList`, `ScanFileWithRegex`, `ConvertMatchListToStringList`, and path helpers. New template helper functions go here.
- Every template must:
  - Declare the three string parameters the task feeds the in-process engine: `OutputFolder`, `ChangeFileManifest`, `GlobalFileManifest` (passed via `AddParameter(null, null, <name>, <value>)`; with `hostSpecific="true"` they resolve through `Host.ResolveParameterValue(null, null, <name>)`).
  - Carry `language="C#" debug="true" hostSpecific="true" langversion="latest"` on the `<#@ template #>` directive — the engine's compiler defaults to C# 5, which cannot compile the string-interpolated template blocks.
  - Include `CodeGenUtilities.ttinclude` and read the dirty file list with `GetFileLinesAsList(ChangeFileManifest)`.
  - Embed comment markers in every generated file so the task can track dependencies: `T4Gen_TemplateFile(<template path>)` and `T4Gen_InputFile(<input path>)` per generated file.
  - Optionally embed `T4Gen_Destination(<folder>)` to override where the task copies the generated file back (default: the test bed project root).
  - Write output into `OutputFolder` with a `.t4generated.<ext>` extension.
- `TestTemplate.tt` — for each dirty non-header input file generates `<Name>_TestHeader.t4generated.h` (commented content dump) plus a `TestTemplate.t4generated.txt` summary.
- `HeaderExample.tt` — header-only generator: scans dirty `.h` files for `T4Gen_RUN_TEXT_TEMPLATE_ON_THIS(<name>)` tags and emits `<Name>_HeaderExample.t4generated.h` containing `static constexpr int <name> = 420;` per tag, copied back next to the source header via `T4Gen_Destination`.
- `TestTemplate.txt` / `HeaderExample.txt` — empty leftovers; unused.

## Work Guidance

## Verification

## Child DOX Index

None.
