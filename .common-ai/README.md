# .common-ai

Shared sources used by the Cursor (`.cursor/`), Claude Code (`.claude/`), and Codex (`.codex/`) skill ecosystems. Everything in this directory is the single source of truth — corresponding files under `.cursor/skills/`, `.claude/skills/`, and `.codex/skills/` are regenerated from here by [scripts/sync-skills.js](scripts/sync-skills.js).

## Layout

```
.common-ai/
├── bootstrap-references/   # Templates the bootstrap skills copy into target projects
├── scripts/
│   └── sync-skills.js      # Regenerates runtime stubs from skills/
└── skills/                 # Canonical SKILL.md + bundled assets per skill
    ├── feature-spec-story-workflow/
    ├── greenfield-spec-bootstrap/
    ├── legacy-spec-bootstrap/
    └── reset-story-or-spec/
```

## How skill propagation works

Cursor, Claude Code, and Codex discover skills by reading `SKILL.md` from their own directory trees (`.cursor/skills/<name>/SKILL.md`, `.claude/skills/<name>/SKILL.md`, and `.codex/skills/<name>/SKILL.md`). These runtimes do not have an `@include` directive, so a real file must exist at those paths.

To keep a single source of truth, the runtime locations hold thin **stubs** containing only:

- The canonical's YAML frontmatter (needed so the runtime can discover the skill and decide whether to trigger it)
- A one-line body telling the agent to read the canonical file and follow it

When a skill triggers, the runtime loads the stub. The stub instructs the agent to read `.common-ai/skills/<name>/SKILL.md`, which contains the actual workflow.

## Editing a skill

1. Edit the canonical file at `.common-ai/skills/<name>/SKILL.md` (or `agents/openai.yaml`, or any bundled `scripts/...`).
2. Run the sync:
   ```sh
   node .common-ai/scripts/sync-skills.js
   ```
3. Commit the canonical change and the regenerated runtime files together.

With the pre-commit hook enabled (see below), step 2 runs automatically.

## Pre-commit hook (recommended)

`.githooks/pre-commit` runs `sync-skills.js` automatically whenever you stage a change under `.common-ai/skills/` or to `sync-skills.js` itself, and stages the regenerated stubs so a single commit covers both the canonical and the propagated changes.

Enable it once per clone:

```sh
git config core.hooksPath .githooks
```

## Adding a new skill that should live in all runtimes

1. Create the canonical directory: `.common-ai/skills/<new-name>/SKILL.md` with frontmatter (`name`, `description`) and full body.
2. Optionally add `agents/openai.yaml` (Codex display metadata) or `scripts/...` (bundled scripts the SKILL.md references via the `.common-ai/...` path).
3. Run `node .common-ai/scripts/sync-skills.js`.
4. Commit.

Skills that only exist in one runtime (for example `skill-creator`, which only lives under `.claude/skills/`) can stay in their runtime directory directly, or have a thin `.cursor/skills/<name>/SKILL.md` stub if they should also be available to Cursor. `sync-skills.js` only processes the directories it finds under `.common-ai/skills/`.

## Removing a skill

The sync script does not delete stale stubs. After removing a canonical skill, also delete its runtime stubs:

```sh
rm -rf .common-ai/skills/<name> .cursor/skills/<name> .claude/skills/<name> .codex/skills/<name>
```

## What the sync script does and doesn't do

- Regenerates the stub `SKILL.md` at `.cursor/skills/<name>/SKILL.md`, `.claude/skills/<name>/SKILL.md`, and `.codex/skills/<name>/SKILL.md` from the canonical frontmatter.
- Copies `.common-ai/skills/<name>/agents/openai.yaml` to `.codex/skills/<name>/agents/openai.yaml` (Codex reads it from that path).
- Does not copy bundled scripts to runtime locations. They live only under `.common-ai/skills/<name>/scripts/`, and the canonical SKILL.md should reference them by that path.
- Does not delete anything. Stale files from removed canonicals must be cleaned up by hand.
