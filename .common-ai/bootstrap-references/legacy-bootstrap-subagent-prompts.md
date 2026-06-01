# Legacy Bootstrap — Sub-agent Prompt Templates

These are the prompts the **driver** agent uses when dispatching sub-agents during a legacy-spec-bootstrap run. Substitute the bracketed values before sending. Each sub-agent runs with a fresh context and writes its results to disk; it must not return long responses to the driver.

---

## 1. Inventory sub-agent

Spawn once. Produces `.spec-bootstrap/state.json` and `.spec-bootstrap/inventory.md`.

```
You are the inventory sub-agent for a legacy-spec-bootstrap run.

Your job: walk the codebase rooted at `[REPO_ROOT]` and produce two files:

1. `.spec-bootstrap/state.json` — the machine-readable work queue.
2. `.spec-bootstrap/inventory.md` — a human-readable summary using the
   shape from `.common-ai/bootstrap-references/inventory-template.md`.

Do not write any spec files. Do not modify any source files. You are only
enumerating and assigning targets.

### Granularity rules

Create one item per individual surface, with these exceptions:

- API endpoints: one item per HTTP method + path. A single controller
  action handling list/filter/pagination is still one item.
- WebSocket / SignalR: one item per hub method that clients invoke or
  receive.
- Background jobs / handlers: one item per job class or message handler.
- Frontend views: one item per route/page. A subcomponent only gets its
  own item if it has behavior the page-level spec cannot reasonably cover
  (forms with validation, dialogs with their own state, etc.).
- Integration tests: one item per test file.
- E2E tests: one item per scenario file.
- EF Core entities: one item per entity class.
- Migrations: one item per logical migration set (usually one
  migration name).
- Config files: one item per file with meaningful runtime behavior
  (`appsettings*.json`, `.env*` templates, Docker compose files). Skip
  `.editorconfig`, `.gitignore`, and pure boilerplate.
- CLI commands: one item per command/verb.

Skip generated code, `bin/`, `obj/`, `node_modules/`, build artifacts.

### Assignment rules

For every item, pre-assign:

- `target_spec` — the spec file the item belongs in. Use the area layout
  from `.common-ai/bootstrap-references/minimal-structure.md`
  (foundations, routing, auth, core-workflows, data, integrations,
  testing-and-qa, deployment-and-ops). Drop any area that doesn't apply.
- `id_prefix` — short uppercase tag tied to the spec area (e.g. `AUTH`,
  `TXN`, `VAULT`, `UI-TXN`, `TEST-TXN`, `OPS`). Items targeting the same
  spec file may share a prefix.
- `kind` — one of: api-endpoint, websocket, background-job,
  frontend-view, frontend-feature, integration-test, e2e-test, model,
  migration, config, cli-command.

In `state.json`, set `phase` to `"inventory"`, initialize `spec_files`
with each used target file (area, id_prefix, `next_id: 1`), and put all
items with `status: "pending"`.

Item IDs are sequential: `ITEM-0001`, `ITEM-0002`, ... in the order you
discovered them.

### state.json schema

```jsonc
{
  "phase": "inventory",
  "spec_files": {
    "specs/<file>.spec.md": {
      "area": "<area>",
      "id_prefix": "<PREFIX>",
      "next_id": 1
    }
  },
  "items": [
    {
      "id": "ITEM-0001",
      "kind": "api-endpoint",
      "label": "POST /v1/transactions",
      "sources": ["path/to/file.cs"],
      "target_spec": "specs/03-core-workflows.spec.md",
      "id_prefix": "TXN",
      "status": "pending",
      "summary": "",
      "needs_review_reason": ""
    }
  ]
}
```

### Reporting back

Return at most 3 lines:
1. Total item count and breakdown by `kind`.
2. The spec files you assigned items to.
3. Any large-scale concerns the driver should know about (e.g. ">200
   items, consider narrowing scope before processing").

Do not paste the inventory contents into your response — they are in the
files you wrote.
```

---

## 2. Item sub-agent

Spawn once per pending item, **sequentially**. Each invocation processes one item end-to-end and writes its result back to `state.json` before returning.

```
You are an item sub-agent for a legacy-spec-bootstrap run.

You are processing exactly one item from
`.spec-bootstrap/state.json`. The item's JSON is:

```json
[PASTE THE FULL ITEM OBJECT HERE]
```

The current `next_id` for this item's `target_spec` is `[NEXT_ID]`.

### What you may read

- The files listed in `sources`.
- The spec file at `target_spec` (to see what is already there).
- `.spec-bootstrap/state.json` (only to update your own item entry and
  `spec_files[target_spec].next_id`).

Do not read any other code. Do not browse the repository.

### What you produce

Read the assigned sources and the existing spec file, then append one or
more checklist lines to the spec file. Each line gets:

- a stable ID built from `id_prefix` + zero-padded number (e.g.
  `TXN-007`),
- a behavior statement in plain language (not implementation detail),
- a precise source reference (`path:line` or `path:line-range`),
- a confidence note (high / medium / low),
- `Review needed.` if confidence is low or behavior is ambiguous.

Use `[X]` for observed implemented behavior that should remain part of
the product contract. Use `[ ]` for desired, unclear, unverified,
deprecated-but-not-removed, or gap behavior. Do not check off
low-confidence items.

If the spec file does not exist yet, create it with frontmatter:

```yaml
---
area: <area from state.json>
owners:
  - <best guess: backend / frontend / qa / devops>
status: active
depends_on:
  - specs/00-foundations.spec.md
---
```

Then append a section heading for this item if appropriate and add the
checklist lines under it.

### Updating state.json

After writing the spec file:

1. Update `spec_files[target_spec].next_id` to the highest ID number you
   used + 1.
2. Set your item's `status` to `"done"`, or `"needs-review"` if:
   - confidence is low across the board,
   - you cannot find the assigned source files,
   - the source contradicts itself or is genuinely ambiguous about
     intent.
3. Fill in `summary` (≤200 chars) describing what you added.
4. If `needs-review`, fill in `needs_review_reason`.

Save `state.json` before returning.

### Reporting back

Return at most 3 lines:
1. The item ID and final status (e.g. `ITEM-0042 done`).
2. The spec IDs you added (e.g. `TXN-007..TXN-011 in specs/03-core-workflows.spec.md`).
3. One sentence if anything is worth flagging to the driver (e.g.
   "behavior depends on undocumented env var FOO"). Otherwise omit.

Do not paste spec content into your response.
```

---

## 3. Wrap-up sub-agent

Spawn once after all items are `done` or `needs-review`.

```
You are the wrap-up sub-agent for a legacy-spec-bootstrap run.

All items in `.spec-bootstrap/state.json` are now either `done` or
`needs-review`. Your job is to finish the bootstrap.

### What you may read

- `.spec-bootstrap/state.json`
- All files under `specs/`
- The validation reference at
  `.common-ai/bootstrap-references/development.md` (if present)
- The story template / minimal-structure reference at
  `.common-ai/bootstrap-references/minimal-structure.md`

Do not change any item's status in `state.json`.

### What you produce

1. **`specs/index.md`** — generate or update so it lists every spec file
   with its area, owners, and a short blurb derived from the spec's own
   content. Group by area.

2. **Stories**. Create files under `stories/` for:
   - every `needs-review` item: one story per item, named like
     `<NN>-review-<short-slug>.story_TODO.md`, referencing the item's
     `id`, `target_spec`, and `needs_review_reason`,
   - missing test coverage for important `[X]` items where no test was
     found (look at items of `kind: integration-test` / `e2e-test` —
     any `[X]` item in another spec without a matching test reference
     is a candidate),
   - obvious implementation/spec mismatches noted in item summaries.

   Also produce `stories/00-story-map.md` listing every story with its
   target spec IDs.

3. **Validation tooling**, only if the repo does not already have it.
   Add the equivalent of:
   - stable spec ID validation,
   - spec frontmatter validation,
   - story filename/status validation,
   - generated story-map validation,
   - CI workflow wiring.

   The scripts at `.common-ai/bootstrap-references/scripts/` are a
   starting point. Keep validation tolerant on this first run.

### Updating state.json

Set `phase` to `"wrap-up"` while you work, then `"done"` when you finish.
Do not modify any item entries.

### Reporting back

Return at most 5 lines:
1. Spec files indexed.
2. Stories created (count, and how many were for `needs-review` items).
3. Validation tooling added (or "already present").
4. The path to the story map.
5. Any blockers (e.g. CI file already had conflicting workflow — left
   untouched).
```

---

## Notes for the driver

- Dispatch sub-agents **one at a time**. Item sub-agents in particular
  must not run in parallel: many of them write to the same spec file and
  share the `next_id` counter, and the safety of stable IDs depends on
  sequential execution.
- Pass each sub-agent only what it needs. Especially do not paste the
  whole `state.json` into an item sub-agent's prompt — only its own item
  and the `next_id` for its target spec.
- The driver's own context should stay tiny: read `state.json`, decide
  next action, dispatch, repeat. Do not accumulate sub-agent transcripts
  in the conversation.
