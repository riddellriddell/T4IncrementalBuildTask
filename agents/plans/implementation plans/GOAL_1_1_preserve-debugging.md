# GOAL 1.1 - Preserve Template Debugging

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `GOAL`
- Task Name: `Keep template debugging working after the in-process engine swap`
- Status: `Draft`
- Owner: `"Your Name"`
- Last Updated: `2026-08-29`

## Linked Context

- Design: [design.md](../../design.md)
- Milestone: [milestones.md](../milestones.md)
- Goal: [goals1.md](../goals1.md) (Goal 1.1, Deliverable 3)
- Handover: `<none>`

## Objective

Ensure the `debug="true"` template directive (both test-bed templates carry it: `HeaderExample.tt:1`, `TestTemplate.tt:1`) keeps producing debuggable generated template code after the engine swap, with no new directives or features introduced. Generated template code remains debuggable exactly as it is today.

## Problem Summary

Today the templates' `debug="true"` directive is honored by `t4.exe`. Under the in-process Mono.TextTemplating engine the debug setting must survive the swap: the directive must still drive a debug-enabled compile (PDB / `#line` mapping) so exceptions and breakpoints land in the `.tt` source. If the engine host strips or ignores the directive, generated-code debugging silently regresses.

## Scope

- In scope: confirm `TemplatingEngine.GetSettings(...)` / directive processing carries `debug="true"` from the template into `TemplateSettings`.
- In scope: where the host configures/overrides settings (per `GOAL_1_1_host-engine-in-process.md`), keep debug options (e.g. `/debug` compiler option, PDB emission, `#line` directives) present for debug-enabled templates and absent for `debug="false"` templates.
- In scope: a repeatable verification pass (debug-binding check) recorded as the acceptance evidence for this deliverable.
- Out of scope: changing the template feature surface (no new directives/features).
- Out of scope: the engine swap itself (Deliverable 1) and failure semantics (Deliverable 4).

## Current State

- Both templates begin with `<#@ template language="C#" debug="true" hostSpecific="true" #>`.
- Today `t4.exe` emits a debuggable compiled template; the test bed does not currently verify debuggability in an automated fashion.
- Mono.TextTemplating's `TemplatingEngine.GetSettings()` builds `TemplateSettings` from the parsed directives, including the `debug` attribute; the in-process Roslyn compile then uses those settings. The risk is only if the host overrides settings with hardcoded values.

## Assumptions and Constraints

- Mono.TextTemplating 3.0.0 supports the `debug` directive attribute through its normal `GetSettings` path.
- In-process Roslyn compilation keeps generated C# and the compiled assembly in memory; debuggability therefore depends on symbols/`#line` directives in the emitted assembly and the runtime's ability to map back to the `.tt` — verify rather than assume.
- No changes to templates are allowed as part of this deliverable.

## Files and Areas Likely Affected

- `CustomBuildTasks/BuildT4TextFiles.cs` - the in-process host added by Deliverable 1 (engine construction and any `TemplateSettings` handling).
- Optional regression fixture only if a manual check cannot be automated — prefer the existing test bed templates, unchanged.

## Implementation Steps

1. **Verify settings propagation.** After Deliverable 1 lands, confirm the engine host produces `TemplateSettings` where `debug="true"` is honored — i.e. the host must not hardcode `CompilerOptions` or disable debug. If the host customizes `TemplateSettings.CompilerOptions` for other reasons, append (not replace) required options and preserve the debug flag.
2. **Confirm symbol/debug output.** With a debug-enabled template compiled through `UseInProcessCompiler()`, confirm the emitted assembly carries debug info (PDB-in-memory or symbol writer) and generated code includes `#line` directives pointing back to the `.tt`.
3. **Confirm debug="false" still skips debug.** Intentionally compile with `debug="false"` (temporary copy or `TemplateSettings` check) and confirm no debug artifacts — the flag must be honored both ways.
4. **Record evidence.** Capture the debug-binding check results (from Manual Checks) in the plan or its Linked docs as the acceptance evidence for the "Debugging preserved" deliverable.

## Verification Plan

### Automated Checks

- `msbuild T4IntegrationTestBed.sln` (Debug config) succeeds with the engine host in place and templates still generating identical outputs.
- `msbuild T4IntegrationTestBed.sln` (Release config) succeeds and produces no debug artifacts for `debug="false"` templates.

### Manual Checks

1. Place a breakpoint inside a `<# ... #>` code block of `HeaderExample.tt` and run the build under the Debugger; confirm the debugger binds to the `.tt` line (not just to raw generated C#).
2. Force a deliberate exception inside a template block and confirm the stack trace maps to a `.tt` line (proves `#line`/PDB behavior).

## Risks and Open Questions

- Risk: in-memory Roslyn compilation may not emit PDBs by default, weakening debugger `.tt` binding; mitigation is explicit debug/symbol settings in the host (step 2).
- Risk: `#line` and symbols can differ between the VS `t4.exe` output and Mono.TextTemplating output; template debugging parity, not byte-identical generated internals, is the bar.
- Question: is debugger binding to `.tt` lines an actual requirement, or is "template can run in Debug with clear errors" sufficient? Confirm with the owner before investing in PDB-emission plumbing.
- Dependency: Deliverable 1 (`GOAL_1_1_host-engine-in-process.md`) must be complete before verification can run.

## Completion Checklist

- [ ] Implementation matches the linked design and goal context
- [ ] Scope stayed within this plan
- [ ] Verification steps were completed or explicitly deferred
- [ ] Relevant status docs were updated
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- This is a verification-heavy deliverable that rides on top of the engine host from Deliverable 1; if the host already passes directives through untouched, this plan may reduce to the verification pass alone.
- Read the Mono.TextTemplating 3.0.0 `TemplateSettings`/`GetSettings` docs before adjusting anything; do not set `CompilerOptions` blindly.
- Leave both templates unchanged; do not add or remove directives.