---
name: reset-story-or-spec
description: >
  Resets a story file back to its TODO state (unchecks all checkboxes and renames the file
  to use the _TODO suffix), or resets one or all spec files (unchecks all checkboxes).
  Use this skill whenever the user asks to: reset a story, undo a story's completion,
  mark a story as TODO/undone/incomplete, reopen a completed story, uncheck a story,
  reset spec checkboxes, uncheck a spec file, or wipe all spec progress. Trigger even
  when the user says things like "I want to redo story 5", "story 12 needs to go back
  to TODO", "clear all the checkboxes in the specs", or "reset story 3 to its initial state".
---

# Reset Story or Spec Skill

This skill resets story and/or spec files in this repository back to their unchecked state.
It covers three distinct operations, all driven by a single Python script.

## What the script does

The script lives at `scripts/reset_story_or_spec.py` relative to this SKILL.md.
Run it from the **repository root** so the `stories/` and `specs/` paths resolve correctly.

| Command | Effect |
|---------|--------|
| `python <script> story <number-or-path>` | Uncheck all `[X]` → `[ ]` in a story file and rename it to `_TODO` |
| `python <script> spec <number-or-path>` | Uncheck all `[X]` → `[ ]` in a spec file (name stays the same) |
| `python <script> all-specs` | Uncheck all `[X]` in every file under `specs/` |
| `python <script> all-stories` | Uncheck all `[X]` in every story file and rename all to `_TODO` |

`<number-or-path>` accepts:
- A bare number like `12` or `05` — the script finds the matching file in `stories/` or `specs/`
- A partial name fragment (for specs, e.g. `tools-and-drawing`)
- A full or relative file path

## When to use each operation

- **Reset a story**: user wants to redo, reopen, or mark a specific story as incomplete.
- **Reset a spec**: user wants to wipe progress on a particular spec (e.g. before re-verifying a feature).
- **Reset all specs**: user wants a clean slate across all specs — common when preparing for a full re-verification pass.
- **Reset all stories**: rare, but use it when the user wants to replay the entire project from scratch.

## How to run it

Determine the right subcommand from the user's request, then run the script. Example:

```bash
# Reset story 12
python .common-ai/skills/reset-story-or-spec/scripts/reset_story_or_spec.py story 12

# Reset spec 03
python .common-ai/skills/reset-story-or-spec/scripts/reset_story_or_spec.py spec 03

# Reset all specs
python .common-ai/skills/reset-story-or-spec/scripts/reset_story_or_spec.py all-specs
```

After running, confirm what changed to the user: list renamed files and how many checkboxes were cleared.
If the user mentioned a specific file by name (e.g. "story 12") verify the rename happened correctly.

## Edge cases

- A story that is already `_TODO` will still have its checkboxes cleared; the file won't be renamed again (it stays `_TODO`).
- The story map file (`00-story-map.md`) is intentionally excluded from `all-stories` resets.
- Spec files have no suffix convention — only their checkboxes are reset, not their name.
- If the user says "all stories" but you're unsure whether they mean it literally, confirm before running `all-stories` since it's a broad operation.
