---
name: legacy-spec-bootstrap
description: Bootstrap a spec-driven workflow for an existing legacy codebase by inventorying implemented behavior, drafting behavior-focused specs with stable IDs and confidence/source notes, creating implementation stories for gaps, and adding validation without treating every implementation detail as desired product truth. Designed to run autonomously over large codebases by enumerating every surface (endpoints, views, tests, models, jobs, migrations, configs) into a coordination state file, then sequentially dispatching one isolated sub-agent per item so the driving agent's context stays small and writes never collide.
---

# Legacy Spec Bootstrap

Use this skill when applying the `specs/` + `stories/` project system to an existing legacy application.

The goal is to produce a reviewed product contract from observed behavior — not to blindly document every implementation detail as desired behavior — while keeping the driving agent's context small enough to run end-to-end on a codebase you cannot fit in one window.

## How this works

The whole workflow is driven by a single coordination file: `.spec-bootstrap/state.json`. The agent running this skill (the **driver**) does not read source code itself. It owns the state file and dispatches sub-agents that each do one isolated piece of work and report back in a few lines.

There are three sub-agent roles:

1. **Inventory sub-agent** — runs once. Walks the codebase and lists every individual surface (one HTTP endpoint, one frontend view, one integration-test file, one EF entity, etc.) into `state.json`. Also produces a human-readable `inventory.md`.
2. **Item sub-agent** — runs once per inventory item, sequentially. Reads only the sources assigned to that item, updates exactly one spec file, then marks the item done (or `needs-review`) in `state.json`.
3. **Wrap-up sub-agent** — runs once. Produces `specs/index.md`, drafts stories for gaps and `needs-review` items, and sets up validation.

The driver's loop is just: read state → spawn next pending item's sub-agent → wait → repeat. Sub-agents run **sequentially, never in parallel**, because many of them write to the same spec files and the sequential order is what guarantees stable IDs and conflict-free appends.

## Core rules

- Do not rewrite the codebase during bootstrap unless explicitly asked.
- Preserve the legacy application's existing source layout. Do not impose greenfield `backend/` or `frontend/` directories on a repo that already has its own structure.
- Do not infer product intent from code without marking the confidence level.
- Separate current behavior, desired behavior, deprecated behavior, and unclear behavior.
- Prefer behavior-focused specs over implementation-shaped specs.
- Keep source references close to discovered requirements so humans can verify them.
- Create stories for missing tests, ambiguous behavior, and implementation/spec alignment work.
- Add spec workflow scaffolding around the existing app: validation scripts, story templates, development docs, and CI integration where they are missing.
- The driver agent must not read source code directly. Anything that requires reading code goes through a sub-agent.

## Coordination files

```
.spec-bootstrap/
├── state.json          # work queue + per-item assignments (machine-readable)
└── inventory.md        # human-readable inventory summary
specs/
└── *.spec.md           # written by item sub-agents
stories/
└── *.story_TODO.md     # written by the wrap-up sub-agent
```

`.spec-bootstrap/` is the durable workspace. If the run is interrupted, the driver picks up by re-reading `state.json` and resuming from the first pending item.

## Project structure setup for legacy repos

Legacy bootstrap adds the spec-driven workflow beside the existing application. It should create or adapt these support paths if they are absent:

```text
.spec-bootstrap/
specs/
stories/
stories/archive/
scripts/
docs/
.github/workflows/       # only when the repository uses GitHub Actions or wants CI added
e2e/                     # only when no e2e scaffold exists and real-stack browser tests are in scope
```

Copy or adapt shared reference templates under `.common-ai/bootstrap-references/`:

- `scripts/validate-workflow.js` — from `.common-ai/bootstrap-references/scripts/validate-workflow.js`
- `scripts/generate-story-map.js` — from `.common-ai/bootstrap-references/scripts/generate-story-map.js`
- `docs/development.md` — from `.common-ai/bootstrap-references/development.md`, edited to match the legacy repo's actual commands and paths discovered during inventory
- root `package.json` — from `.common-ai/bootstrap-references/package.json` only if the repo has no root package file; otherwise merge in `validate:workflow` and `generate:story-map` without overwriting existing scripts
- root `AGENTS.md` — read `.common-ai/bootstrap-references/AGENTS.md` and merge its spec/story/testing/agent guidance into the current root `AGENTS.md`, preserving existing project-specific guidance
- `stories/_template.story_TODO.md` — story template referenced by future stories

Do not initialize new app projects during legacy bootstrap. If the legacy code already lives in `src/`, `Waterblocks.Api/`, `web/`, `client/`, or another established layout, document that layout in `docs/development.md` and point stories at the existing paths. If backend, frontend, database, Docker, or CI foundations are missing, create TODO stories for that work rather than creating the implementation directly.

If the product purpose is discoverable from README files, route names, tests, or code comments, draft `docs/product-vision.md` as an observed product summary with confidence notes and open questions. If intent is not clear, create a short placeholder that says the product vision needs human review rather than guessing.

When merging `AGENTS.md`, do not overwrite legacy instructions, architecture notes, or contributor guidance. Add only missing workflow guidance from the reference, reconcile duplicates into one clear section, and update `Current Spec Files` after the inventory has determined which spec files exist.

## state.json schema

```jsonc
{
  "phase": "inventory" | "processing" | "wrap-up" | "done",
  "spec_files": {
    "specs/02-auth-and-identity.spec.md": {
      "area": "auth",
      "id_prefix": "AUTH",
      "next_id": 7
    }
  },
  "items": [
    {
      "id": "ITEM-0001",
      "kind": "api-endpoint",          // api-endpoint | websocket | background-job | frontend-view | frontend-feature | integration-test | e2e-test | model | migration | config | cli-command
      "label": "POST /v1/transactions",
      "sources": [
        "Waterblocks.Api/Controllers/TransactionsController.cs"
      ],
      "target_spec": "specs/03-core-workflows.spec.md",
      "id_prefix": "TXN",
      "status": "pending",             // pending | in-progress | done | needs-review
      "summary": "",                   // filled in by the item sub-agent (≤200 chars)
      "needs_review_reason": ""        // filled in only when status=needs-review
    }
  ]
}
```

`id_prefix` is pre-assigned per item by the inventory pass so that items mapping to the same spec file don't collide. `next_id` on each spec file tracks the next available number for that prefix; item sub-agents increment it.

## The driver loop

The driver agent (you, when invoked) runs this loop. Do not read any source files yourself.

```
1. If `.spec-bootstrap/state.json` does not exist:
     spawn the inventory sub-agent.
     return — the loop continues on the next user turn or via the loop skill.
2. Read `.spec-bootstrap/state.json`.
3. If phase == "inventory":
     it means the inventory sub-agent just finished. Set phase = "processing", save.
4. If phase == "processing":
     find the first item with status == "pending".
     if none: set phase = "wrap-up", save, go to step 5.
     else: mark that item in-progress, save, spawn the item sub-agent for it,
           wait for it to finish, re-read state.json, loop back to step 4.
5. If phase == "wrap-up":
     spawn the wrap-up sub-agent. When it finishes, set phase = "done", save.
6. If phase == "done":
     produce the final summary for the user (≤200 words):
       - total items processed, broken down by status
       - list of `needs-review` items and their reasons
       - paths to inventory.md, specs/index.md, stories/00-story-map.md
```

The driver should call sub-agents one at a time and keep its own responses terse. Between item sub-agents the driver should output at most one short status line (e.g. `"ITEM-0042 done (frontend-view: TransactionsList)"`) so the user can follow progress without bloating the conversation.

If a sub-agent reports an unrecoverable error (e.g. cannot find the assigned source file), the driver marks that item `needs-review` with the error message in `needs_review_reason` and continues. Do not halt the whole run for a single bad item.

## Sub-agent dispatch

Spawn sub-agents via the platform's task / sub-agent mechanism (e.g. the `Agent` / `Task` tool). Use the prompt templates in `.common-ai/bootstrap-references/legacy-bootstrap-subagent-prompts.md`. Pass each sub-agent only the information it needs:

- **Inventory sub-agent**: the repo root, the path to write `state.json` and `inventory.md`, the schema above, and the granularity rules below.
- **Item sub-agent**: the single item's JSON object, the path to `state.json`, and the relevant spec file's current `next_id`. It must read only `sources`, only the assigned spec file, and only the state file.
- **Wrap-up sub-agent**: paths to all spec files and `state.json`. It must not change any item's status.

Sub-agents return a ≤3-line plain-text summary. Anything substantive they discovered should already be in the files they wrote.

## Granularity (used by the inventory sub-agent)

Aim for one item per individual surface, but merge trivially-paired items so the queue does not explode:

- **API endpoints**: one item per HTTP method+path. If a single controller action handles a list+filter+pagination as one cohesive behavior, that is still one item.
- **WebSocket / SignalR**: one item per hub method that the client can invoke or receive.
- **Background jobs / handlers**: one item per job class or message handler.
- **Frontend views**: one item per route/page. Subcomponents inside that page only get their own item if they have meaningful behavior the page-level spec cannot cover (forms with validation, dialogs with their own state, etc.).
- **Integration tests**: one item per test file (group `[Fact]`s by file, not by method).
- **E2E tests**: one item per scenario file.
- **EF Core entities**: one item per entity class.
- **Migrations**: one item per logical migration set (the inventory sub-agent decides what counts as logical — usually one migration name).
- **Config files**: one item per file with meaningful runtime behavior (`appsettings*.json`, `.env*` templates, Docker compose files). Skip pure boilerplate (`.editorconfig`, `.gitignore`).
- **CLI commands**: one item per command/verb.

Skip generated code, `bin/`, `obj/`, `node_modules/`, build artifacts.

The inventory pass also pre-assigns each item to a `target_spec` and `id_prefix`. Use the area-based spec layout from `.common-ai/bootstrap-references/minimal-structure.md` as the default carving (foundations, routing, auth, core-workflows, data, integrations, testing-and-qa, deployment-and-ops). Drop any area the codebase doesn't have. The `id_prefix` should be a short uppercase tag tied to the spec area (e.g. `AUTH`, `TXN`, `VAULT`, `UI-TXN`, `TEST-TXN`, `OPS`).

## What item sub-agents produce

For each item the sub-agent reads its assigned sources and updates exactly one spec file. The spec file uses the existing frontmatter and checklist conventions:

```yaml
---
area: core-workflows
owners:
  - backend
status: active
depends_on:
  - specs/00-foundations.spec.md
---
```

Each checklist line gets a stable ID, source, and confidence:

```md
- [X] TXN-007: Submitting a transaction returns 202 with a transaction ID. Source: `Waterblocks.Api/Controllers/TransactionsController.cs:42-58`. Confidence: high.
- [ ] TXN-008: Transactions in BROADCASTING state are retried up to 3 times. Source: inferred from `RetryPolicy.cs:18`. Confidence: medium. Review needed.
```

- `[X]` for observed implemented behavior that should remain part of the contract.
- `[ ]` for desired, unclear, unverified, deprecated-but-not-yet-removed, or gap behavior.

Confidence levels:

- `Confidence: high` — confirmed by tests, UI behavior, or clear code paths.
- `Confidence: medium` — inferred from code/config but not observed end-to-end.
- `Confidence: low` — based on naming, dead-looking code, comments, or partial paths. Do not check these off; mark them `Review needed` and have the sub-agent set the item's status to `needs-review`.

When the sub-agent finishes it must:

1. Append the new lines to the assigned spec file (creating the file with frontmatter if it does not yet exist).
2. Update `spec_files[<target_spec>].next_id` in `state.json` to reflect the highest ID used + 1.
3. Set this item's `status` to `done` or `needs-review`, fill in `summary` (≤200 chars), and `needs_review_reason` if applicable.
4. Return a ≤3-line plain-text summary.

## Wrap-up phase

When all items are processed the driver dispatches the wrap-up sub-agent, which:

1. Generates or updates `specs/index.md` from the populated spec files.
2. Ensures the workflow scaffold exists without disturbing existing app code:
   - `stories/_template.story_TODO.md` and `stories/archive/`,
   - `scripts/validate-workflow.js` and `scripts/generate-story-map.js`,
   - root `package.json` scripts for `validate:workflow` and `generate:story-map` (merge, do not overwrite),
   - root `AGENTS.md` merged with `.common-ai/bootstrap-references/AGENTS.md`,
   - `docs/development.md` tailored to the actual legacy commands and paths,
   - `docs/product-vision.md` if enough product intent was observed to draft it safely.
3. Creates stories under `stories/` for:
   - every `needs-review` item (one story per item, referencing its spec ID),
   - missing test coverage for important `[X]` items where no test was found,
   - obvious implementation/spec mismatches called out by item sub-agents in their summaries,
   - missing project foundations that greenfield bootstrap would normally create up front but the legacy repo lacks, such as CI, Docker Compose, database migrations, or real-stack e2e smoke coverage.
4. Runs or wires story-map generation so `stories/00-story-map.md` reflects the current active and archived stories.
5. Adds validation tooling if the repo does not already have it (stable-ID validation, frontmatter validation, story filename/status validation, story-map generation, CI wiring). Keep validation tolerant on the first bootstrap, then tighten after the baseline is clean.
6. Adds or updates CI only by integrating with the repository's existing pipeline style. For GitHub Actions, make the workflow run the existing build/test commands plus `npm run validate:workflow`; add Playwright real-stack e2e steps only when the required backend, database, and frontend startup commands are known.

The wrap-up sub-agent does not change any item's status. It only consumes the state file.

## Output expectations

When the run finishes the user should have:

1. `.spec-bootstrap/state.json` and `.spec-bootstrap/inventory.md`.
2. Populated spec files under `specs/` with frontmatter, stable IDs, sources, and confidence notes.
3. A populated `stories/` directory including `00-story-map.md` and one story per `needs-review` item.
4. Workflow support files: `stories/_template.story_TODO.md`, validation and story-map scripts, root workflow commands, and updated `AGENTS.md`.
5. `docs/development.md` reflecting the legacy repo's actual commands and, when safe, `docs/product-vision.md` with observed intent and open questions.
6. Validation commands and CI wiring when the repo did not already have them, plus e2e scaffolding only when it can run against the real existing stack.
7. A short final summary from the driver listing total items, status breakdown, and the items that need human review.

## When the codebase is genuinely huge

If the inventory sub-agent reports more than ~150 items the driver should pause after the inventory phase and offer the user a choice:

- proceed with the full list (will take a long time but is autonomous),
- narrow scope to specific spec areas first (e.g. routing + auth + one core workflow), then expand later,
- merge fine-grained items into coarser ones for the first pass and refine later.

This is the only point in the workflow where the driver should solicit user input. Everything else runs autonomously off `state.json`.

## Re-running

The skill is idempotent. If `.spec-bootstrap/state.json` already exists when the driver starts, it picks up from the first non-`done` item. To force a clean rerun, the user deletes `.spec-bootstrap/` (or just `state.json`) before invoking the skill.

## Reference files

- `.common-ai/bootstrap-references/legacy-bootstrap-subagent-prompts.md` — exact prompt templates for the inventory, item, and wrap-up sub-agents.
- `.common-ai/bootstrap-references/inventory-template.md` — shape of the human-readable `inventory.md` summary.
- `.common-ai/bootstrap-references/minimal-structure.md` — default spec area layout and owner values to use for `target_spec` assignment.
