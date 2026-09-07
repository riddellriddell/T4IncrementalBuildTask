# Implementation Plan: Semantic-Version Tags + Release Notes (pin to tags, never bare SHAs)

version: 0.0
owner: "Your Name"
repo: "riddellriddell/T4IncrementalBuildTask"

---

## Metadata

- Task Type: `FEATURE`
- Task Name: Semantic version tags + release notes; consumers pin to tags
- Status: `Draft` (2026-09-07)
- Owner: "Your Name"
- Last Updated: `2026-09-07`

## Linked Context

- Design: [design.md](../design.md)
- Workflow: [buildguild.md](../../buildguild.md)
- Milestone: [milestones.md](../milestones.md) (Milestones 1 and 2 both **Complete**; this plan's initial tag maps to that state)
- Goal: [goals1.md](../goals1.md), [goals2.md](../goals2.md) (content source for the initial release notes)
- Handover: n/a (single-phase change)

## Objective

Establish three durable, documented artifacts/conventions in this repository:

1. A **SemVer 2.0.0 tagging scheme** (`vX.Y.Z`, annotated tags, cut at the repo root on `main`).
2. A **release-notes artifact** — a new root `CHANGELOG.md` — that tracks every version: an `Unreleased` section for in-flight work and one dated section per cut tag, composed from milestone/goal history and git log.
3. An explicit **pin-to-tag rule**: everywhere downstream consumption is documented (consumer-facing docs, plan docs, this repo's own AGENTS chain), consumers **pin to a `vX.Y.Z` tag (or the GitHub Release tied to it), never a bare commit SHA or an unlabelled `main` head** — and propose the exact initial tag (`v0.1.0`) plus the commit it will be cut at.

## Problem Summary

- The repository currently has **no tags at all** (`git tag -l` returns empty). There is no stable reference a downstream consumer can name.
- The only downstream-consumption text in the repo is `T4CodeGen/README.md` (a "second front-end… `T4CodeGen.exe`" alongside the MSBuild task, meant to be integrated into cmake/CI builds per commit `6f71710`). Without a tag convention, the only way a consumer can "pin" this tooling is to copy **an arbitrary commit hash** — no stable reference, no release-note trail, no signal about which version got which behavior (in-process engine vs standalone API vs CLI, failure isolation, wildcard paths).
- There is no release/log artifact: no `CHANGELOG.md`, no `RELEASES.md` at the repo root (confirmed). A reader of `milestones.md` can reconstruct history, but nothing is versioned or referencable.
- Because Milestones 1 and 2 are both Complete (engine + standalone API + CLI), there is a well-defined first baseline to tag — the project is in the unusual position of having a stable, working pipeline with **zero tagged history**.

## Scope

- In scope: define and document the versioning scheme (SemVer 2.0.0, `v`-prefixed, annotated tags, repo-root tags on `main`).
- In scope: create the root `CHANGELOG.md` release-notes artifact (Keep-a-Changelog structure) with an `Unreleased` section and the initial `v0.1.0` section composed from `milestones.md` / `goals1.md` / `goals2.md` / `git log`.
- In scope: propose the exact initial tag — name `v0.1.0`, commit = the landing HEAD of this plan — and justify the version choice against the completed milestones.
- In scope: document the pin-to-tag convention at every place downstream consumption is documented: `T4CodeGen/README.md` (primary consumer doc), root `AGENTS.md` (project convention), and the plans index (`agents/plans/AGENTS.md`).
- In scope: document the **future-version workflow** (feature lands → `Unreleased` entry edited in the same change set → at cut time, fold to `vX.Y.Z` + annotated tag + optional GitHub Release), including the exact `git tag -a` command as documentation.
- Out of scope: **literally running `git tag`** or pushing a tag — the cut happens as a post-land step by the maintainer, not in this plan.
- Out of scope: creating the **GitHub Release with a binary asset** — that is a separate "prebuilt artifact" plan (tag-exe/assembly-version alignment is likewise a separate decision; see Risks).
- Out of scope: publishing `CustomBuildTasks.dll` / `T4CodeGen.exe` to a package feed (NuGet) or adding CI-based changelog/tag automation (repo builds offline, no workflow).
- Out of scope: rewriting history or back-dating tags to commits that predate this plan.

## Current State

- `git tag -l` is **empty**; default branch is `main`; `origin` = `riddellriddell/T4IncrementalBuildTask`.
- Git log (relevant tail): `b2b97d7` "added plan for wildcard support" (HEAD), `5fd361d` "Land FEATURE_exe-test-project" (test harness), `6f71710` "implemented a standalone exe version to make it easy to integrate into a cmake build", `cf020af` "Land Goal 2.1", `d9e0187` "Land Goal 1.1", `ec945c1` initial Mono.TextTemplating swap, `8cf3e93` initial commit.
- Milestones 1 (in-process engine) and 2 (standalone API + CLI) are **Complete** per `milestones.md`; the only in-flight item is the `FEATURE_wildcard-path-support` plan (**Draft** 2026-09-05, not landed).
- No `CHANGELOG.md` or `RELEASES.md` exists at the root (glob-confirmed). `.gitignore` ignores `[Rr]eleases/` (a **directory** pattern) and the historical Gist-style `[Rr]elease/` build-output dirs, but a tracked root file named `CHANGELOG.md` is **not** affected by any ignore rule — no `.gitignore` change required.
- No existing doc tells a consumer to pin a specific SHA (searched `*.md` for `commit`/`SHA`/`tag`/`release`); the gap is *forward-looking* — consumer-facing docs simply never address versioning. The task framing previously told users effectively "pin commit `<hash>`"; that instruction now needs to exist as "pin `vX.Y.Z`".
- No markdownlint config file exists (goals files carry inline `markdownlint-disable` comments); keep new markdown lint-clean to the same width/format conventions.

## Assumptions and Constraints

- Versioning follows **SemVer 2.0.0** with a `v` prefix: `v<MAJOR>.<MINOR>.<PATCH>`. Pre-1.0 (see Risks) minor bumps denote feature additions; patch bumps denote bugfix-only changes.
- Tags are **annotated** (`git tag -a vX.Y.Z -m ...`), carrying the release notes at the tagged commit; lightweight tags are not used for releases.
- A tag describes the **whole repo state** at repo root: this repository hosts the MSBuild task, the standalone API, the CLI, and the test harness, so `vX.Y.Z` names one snapshot of all of them. The standalone-consumer story (a downloadable, prebuilt exe per version) is covered by the separate release-artifact plan, not by this tagging convention.
- `CHANGELOG.md` is the **source of truth** for release notes in this repo; a GitHub Release, when one is created, mirrors it (copy, not a second editorial source).
- Changelog discipline is manual and offline-friendly (no toolchain): every change set that affects behavior also updates the `Unreleased` section of `CHANGELOG.md` in the same commit.
- The initial tag is cut at the **post-land HEAD** of this plan (the commit that introduces `CHANGELOG.md`), so the tag, the notes, and the code all describe the same state.
- This plan may edit documentation only; no source binaries, csproj, targets, templates, or test fixtures change.

## Files and Areas Likely Affected

- `CHANGELOG.md` (new, root) — the release-notes artifact: `Unreleased` section + first dated `v0.1.0` section + a "Release conventions" block (semver scheme, annotated-tag procedure, pin-to-tag rule, future-version workflow). Not affected by `.gitignore`.
- `T4CodeGen/README.md` — primary downstream-consumer doc; add a `Versions / Pinning` section: "pin to a `vX.Y.Z` tag (or the GitHub Release tied to it), never a bare SHA"; point at `CHANGELOG.md`.
- `AGENTS.md` (root) — project conventions + root-owned file list: record the tagging scheme, the pin-to-tag rule, and the new `CHANGELOG.md` as a root-owned file (Child DOX Index).
- `agents/plans/AGENTS.md` — plans index: add the `FEATURE_semantic-tags-release-notes.md` entry (Draft 2026-09-07).
- `agents/plans/milestones.md` *(optional)* — a one-line note mapping the completed milestones to the `v0.1.0` baseline (the milestone text itself stays authoritative; no rewrite).
- `.gitignore` — **no change expected**; verify only (a root trackable `CHANGELOG.md` is not ignored; the `[Rr]eleases/` rule applies to a directory, not `.md` files).

## Implementation Steps

1. **Define the versioning scheme.** Write the "Release conventions" block (as the top-matter of the new `CHANGELOG.md`): SemVer 2.0.0, `v` prefix, annotated tags at repo root on `main`, one section per version, `Unreleased` for in-flight work, and the rule that behavior changes update `Unreleased` in the same change set.
2. **Compose the initial release notes and create `CHANGELOG.md`.** Ground the `v0.1.0` section in the milestone/goal history: Milestone 1 (in-process Mono.TextTemplating engine + Roslyn, vendored under `tools\`, `t4.exe`/`powershell.exe` shell-out removed, `ChangeFileManifest` rename, per-template failure isolation) and Milestone 2 (standalone `TemplateCompiler` API, thin MSBuild task adapter, `T4CodeGen.exe` CLI + exit-code semantics) plus the landed `T4CodeGenTests` harness (2026-09-05). Write an `Unreleased` section listing the in-flight wildcard-path support. Preserve the repo's heading/width conventions and keep it lint-clean (no inline `markdownlint-disable` blanket).
3. **Document the pin-to-tag rule in the consumer doc.** In `T4CodeGen/README.md`, add a `Versions / Pinning` section: downstream consumers (cmake/CI including the integration the exe was built for) must pin to a `vX.Y.Z` tag or the GitHub Release tied to it; the version table/`.rsp` examples stay tag-neutral; explicitly state "do not pin a raw commit SHA". Reference `CHANGELOG.md`.
4. **Record the convention in the DOX chain.** Update root `AGENTS.md` (project conventions + root-owned file list) with the tagging scheme and pin-to-tag rule, and add the `CHANGELOG.md` root-owned entry. Add the plan entry to `agents/plans/AGENTS.md`. Optionally add the milestone→`v0.1.0` mapping note in `milestones.md`.
5. **Propose the exact initial tag.** Name: **`v0.1.0`**. Commit: the **landing HEAD of this plan** (tag and CHANGELOG land together so the tag describes its own notes). Justification: SemVer's `0.y.z` range is the correct home for a project whose public `TemplateCompiler` API is still actively shaped (the Draft wildcard plan changes `Compile`'s input normalization) and that makes no 1.0 compatibility promise; the completed Milestones 1+2 define the content, not a 1.0 claim. When the API settles (e.g. wildcard support lands and is declared stable), cut `v1.0.0` per SemVer.
6. **Document the cut + future-version procedure (do not execute).** In the release conventions block, record the exact cut steps for the maintainer: fold `Unreleased` into a `## [vX.Y.Z] - YYYY-MM-DD` section, `git add CHANGELOG.md`, commit, `git tag -a vX.Y.Z -m "..."` at that commit, `git push origin main --tags`, then (as the separate artifact plan) create the GitHub Release mirroring the notes. Future versions: `Unreleased` edit happens in the feature's own change set; version bumps follow SemVer from the last tag.

## Verification Plan

### Automated Checks

- `git tag -l` — after the deferred cut, returns at least `v0.1.0`; before the cut it may remain empty (the check that matters is the tag/convention *documents* exist and are consistent). This plan adds no CI, so a manual consistency check stands in.
- Changelog consistency check (one-off `pwsh`/`rg`, offline): parse `CHANGELOG.md` for `^## \[v[0-9]+\.[0-9]+\.[0-9]+\]` — the top released version must follow the semver regex and must be the newest by SemVer ordering; every released section carries a date.
- Grep sweep (`rg -n "sha" --glob "*.md"`) in `T4CodeGen/`, `agents/` and root docs — after the change, **no** consumer-facing doc instructs (or permits) pinning a bare SHA; any hit must be the new explicit prohibition or an unrelated word.
- `git log --oneline` cross-tabulated against the `v0.1.0` section: every landed milestone/plan commit (`cf020af`, `d9e0187`, `5fd361d`, `6f71710`, …) is accounted for in a version section.
- Markdown lint: no repo markdownlint config exists, so a manual structural check of `CHANGELOG.md` (heading hierarchy, numbered/`-` lists, no long lines beyond existing doc style) is the automated-equivalent; no new tooling is added.

### Manual Checks

1. Read the `v0.1.0` section against `git log --oneline` and `milestones.md`/`goals1.md`/`goals2.md`: every claim traces to a landed plan/goal; no invented history.
2. Open `T4CodeGen/README.md`: the `Versions / Pinning` section says "pin to a `vX.Y.Z` tag or GitHub Release" and tells readers **not** to use a raw SHA; the worked example and exit-code docs are unchanged otherwise.
3. Open root `AGENTS.md` + `agents/plans/AGENTS.md`: the tagging convention, pin rule, and this plan's index entry are present and coherent with the existing DOX style.
4. After the maintainer executes the deferred cut: `git tag -l`, `git describe`, and `git show v0.1.0` — annotated tag at the post-land HEAD naming `CHANGELOG.md`; then confirm `git log v0.1.0..HEAD --oneline` shows only post-release work captured in `Unreleased`.

## Risks and Open Questions

- Risk: **initial version choice leapfrog** — a milestone-matched `v1.0.0` would imply API stability the project hasn't committed to, while `v0.1.0` is a safe pre-1.0 baseline. Decision in this plan: `v0.1.0`; revisit `v1.0.0` when the API is declared stable. If the owner prefers a 1.0 flank, promote the baseline to `v1.0.0` in the same step — but say so explicitly, since `0.x` vs `1.x` is a public-API promise, not a milestone counter.
- Question: **`CHANGELOG.md` vs GitHub Releases as the notes home?** Decision: `CHANGELOG.md` is the source of truth; GitHub Releases mirror it (never two editorial sources). Revisit only if the prebuilt-artifact plan makes the GitHub Release page the primary consumer surface.
- Question: **exe/assembly version alignment** — a repo tag names a snapshot, but `T4CodeGen.exe` has no per-version file/assembly version today; aligning the exe's `AssemblyVersion`/file version with `vX.Y.Z` is a separate change (needs `AssemblyInfo` edits per release) to be planned with the prebuilt-artifact plan.
- Risk: **version-notes drift** — future features that forget the `Unreleased` edit recreate today's gap. Mitigation: the root `AGENTS.md` rule + `CHANGELOG.md` release-conventions block make the same-change-set edit a contract (DOX pass covers it).
- Dependency: the deferred `git tag -a` + push is a manual post-land step by the maintainer (`origin` = `riddellriddell/T4IncrementalBuildTask`); this plan only documents it. If a remote push is undesired, tags still work locally — say so at cut time.

## Completion Checklist

- [ ] Implementation matches the linked design and goal context
- [ ] Scope stayed within this plan
- [ ] Verification steps were completed or explicitly deferred
- [ ] Relevant status docs were updated
- [ ] A handover document was created if the work stopped mid-phase

## Notes for the Implementing Agent

- Read the DOX chain first (`AGENTS.md` root → `agents/plans/AGENTS.md` → `T4CodeGen/AGENTS.md`) and re-run the same git/glob probes used here before editing.
- There is **no** `CHANGELOG.md`/`RELEASES.md` today and **no** markdownlint config; create the changelog at repo root (it is not ignored), match the repo's heading/format conventions, and do not add any lint/build tooling.
- Compose `v0.1.0` notes only from what `milestones.md`, `goals1.md`, `goals2.md`, and `git log --oneline` actually support; do not invent pre-`8cf3e93` history or back-date tags.
- `T4CodeGen/README.md` is the one genuine consumer-facing doc — make the pin-to-tag rule explicit there, and record the rule in root `AGENTS.md` so the DOX pass keeps it (the "same change set updates `Unreleased`" contract lives in both).
- Do **not** run `git tag`, push anything, or create a GitHub Release — this plan documents the cut procedure; execution is the maintainer's post-land step. The separate release-artifact plan owns binaries and exe-version alignment.
- Keep the tag/version table truthful to a single-repo snapshot: one `vX.Y.Z` labels the MSBuild task, the API, the CLI, and the tests at once.