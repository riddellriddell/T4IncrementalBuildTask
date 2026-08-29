# agents

## Purpose

OMP-native skills location. The `agents` provider discovers skills here as `agents/skills/<skill-name>/SKILL.md` (non-recursive: one level under `skills/`). Each skill is a `SKILL.md` plus optional asset files, loaded on demand via `skill://<name>` and injectable by the user via `/skill:<name>`.

## Ownership

- Owned by this folder.
- Project-scoped: any OMP session opened at this repo root discovers these skills.

## Local Contracts

- One directory per skill: `agents/skills/<skill-name>/SKILL.md`. Nested paths (e.g. a vendored `engineering/code-review`) are not discovered — flatten to one level.
- `SKILL.md` frontmatter must carry a meaningful `name` (defaults to the directory name) and `description`.
- All skills here are **user-injectable only**: frontmatter sets `hide: true` and `disable-model-invocation: true`, so none appear in the model's system-prompt skill list and none are model-invocable. Each loads only through the user's `/skill:<name> [args]` (requires `skills.enableSkillCommands`) or `skill://<name>`.
- Vendored from `mattpocock/skills`, copied verbatim (assets included) with `hide: true` (and, where missing, `disable-model-invocation: true`) added to the frontmatter:
  - `code-review`, `grilling`, `writing-for-agents` are the upstream **model-invoked** set, flattened from `skills/engineering/code-review` and `skills/productivity/*`.
  - The other 14 are the upstream **user-invoked** set, flattened from `skills/engineering/*` and `skills/productivity/*`. The full `skills/productivity/*` set is present.
- Invocation modes:
  - Orchestrators (drive other skills via the Skill tool / reference other skills): `ask-matt`, `grill-me`, `grill-with-docs`, `implement`, `improve-codebase-architecture`, `to-spec`, `to-tickets`, `triage`, `wayfinder`.
  - Primitives (model-invoked upstream; here called explicitly via the Skill tool by the orchestrators, still loadable through `skill://<name>`): `grilling`, `writing-for-agents`.
  - Self-contained: `code-review`, `handoff`, `teach`, `to-questionnaire`, `wait-what`.
  - Bootstrap: `setup-matt-pocock-skills` scaffolds the issue tracker + triage label + domain doc config that the engineering skills assume. Run it to create `docs/agents/issue-tracker.md`; until it is run, tracker-dependent steps (e.g. `code-review` spec fetch) degrade to asking the user.
- **Dependency note:** the orchestrators call model-invoked skills from the upstream repo that are **not** vendored here (`domain-modeling`, `codebase-design`, `tdd`, `prototype`, `diagnosing-bugs`, `research`, `resolving-merge-conflicts`, `wizard`). Until those are vendored too, such references dangle; the orchestrator should fall back to doing the equivalent work inline. Do not vendor the model-invoked set without asking the user.

## Work Guidance

## Verification

## Child DOX Index

- `buildguild.md` — build/test recipe for the project (canonical build & verify flow, Verification Bar, gotchas, and a current-status banner on the blocked end-to-end build until Goal 1.1 lands). Root `AGENTS.md` points at it.
- `plans/` — planning and design docs for this repo (`design.md`, `implementation plans/`). See `plans/AGENTS.md`.
- `skills/code-review/` — two-axis (Standards + Spec) diff-review skill, vendored from `mattpocock/skills` (`skills/engineering/code-review`). User-injectable only.
- `skills/ask-matt/` — router over the skills (`PHASE-BOUNDARIES.md`), vendored from `skills/engineering/ask-matt`. User-injectable only.
- `skills/grill-me/` — stateless grilling wrapper (calls the `grilling` Skill), vendored from `skills/productivity/grill-me`. User-injectable only.
- `skills/grill-with-docs/` — grilling that also builds the domain model (calls `grilling` + `domain-modeling`), vendored from `skills/engineering/grill-with-docs`. User-injectable only.
- `skills/grilling/` — the interview primitive (design tree, rounds, frontier) that `grill-me`, `grill-with-docs`, `triage`, `wayfinder` and `improve-codebase-architecture` call; vendored from `skills/productivity/grilling`. Model-invoked upstream, adapted to user-injectable here.
- `skills/handoff/` — conversation compaction into a portable doc, vendored from `skills/productivity/handoff`. User-injectable only.
- `skills/implement/` — build-from-spec builder (drives `/tdd`), vendored from `skills/engineering/implement`. User-injectable only.
- `skills/improve-codebase-architecture/` — deepening-opportunities survey + HTML report (`HTML-REPORT.md`), vendored from `skills/engineering/improve-codebase-architecture`. User-injectable only.
- `skills/setup-matt-pocock-skills/` — per-repo config bootstrap (`domain.md`, `issue-tracker-*.md`, `triage-labels.md`), vendored from `skills/engineering/setup-matt-pocock-skills`. User-injectable only.
- `skills/teach/` — stateful multi-session teaching (`GLOSSARY-FORMAT.md`, `LEARNING-RECORD-FORMAT.md`, `MISSION-FORMAT.md`, `RESOURCES-FORMAT.md`), vendored from `skills/productivity/teach`. User-injectable only.
- `skills/to-questionnaire/` — questionnaires for the one person who can answer, vendored from `skills/productivity/to-questionnaire`. User-injectable only.
- `skills/to-spec/` — conversation-to-spec synthesis for the issue tracker, vendored from `skills/engineering/to-spec`. User-injectable only.
- `skills/to-tickets/` — plan/spec into tracer-bullet tickets with blocking edges, vendored from `skills/engineering/to-tickets`. User-injectable only.
- `skills/triage/` — triage state machine over issues/PRs (`AGENT-BRIEF.md`, `OUT-OF-SCOPE.md`), vendored from `skills/engineering/triage`. User-injectable only.
- `skills/wait-what/` — mid-conversation re-pitch with the shared vocabulary, vendored from `skills/productivity/wait-what`. User-injectable only.
- `skills/wayfinder/` — decision-ticket map for oversized efforts, vendored from `skills/engineering/wayfinder`. User-injectable only.
- `skills/writing-for-agents/` — reference for writing any doc an agent consumes (`SKILL-MECHANICS.md`), vendored from `skills/productivity/writing-for-agents`. Model-invoked upstream, adapted to user-injectable here.