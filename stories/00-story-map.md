# Story Map

This map lists every story under `stories/`. New stories follow `NN-slug.story_TODO.md` / `.story_DONE.md`. Pre-existing feature folders (`admin-fees/`, `memo-based-blockchains/`) follow a different convention and predate the spec-bootstrap workflow; they are listed here for visibility.

## Active Stories (spec-bootstrap)

| Story | Target Specs | Theme |
| --- | --- | --- |
| `stories/10-e2e-tests-for-admin-ui-workflows.story_TODO.md` | `specs/06-testing-and-qa.spec.md`, `specs/01-routing-and-navigation.spec.md`, `specs/03-core-workflows.spec.md` | Test coverage gap (UI e2e) |
| `stories/11-frontend-unit-tests-for-admin-client-and-hooks.story_TODO.md` | `specs/06-testing-and-qa.spec.md`, `specs/00-foundations.spec.md` | Test coverage gap (frontend unit) |
| `stories/12-tests-for-signalr-realtime-channel.story_TODO.md` | `specs/06-testing-and-qa.spec.md`, `specs/05-integrations.spec.md` | Test coverage gap (SignalR) |
| `stories/13-scope-unspent-inputs-to-workspace.story_TODO.md` | `specs/03-core-workflows.spec.md` | Implementation/spec mismatch |
| `stories/14-clean-up-unused-fireblocks-asset-seed.story_TODO.md` | `specs/04-data-and-persistence.spec.md` | Code/spec mismatch |

## Pre-existing Feature Stories

- `stories/admin-fees/` (5 stories + README) — fee surfacing in admin UI.
- `stories/memo-based-blockchains/` (5 stories + Plan.md + README) — memo/tag support for MemoBased assets.

## Story Number Conventions

- Numbers `01-09` are reserved for the recommended scaffolding sequence from `.common-ai/bootstrap-references/minimal-structure.md` (not applicable to this brownfield repo, which is already scaffolded).
- Spec-bootstrap-derived stories start at `10`.
- Existing topical subfolders (`admin-fees/`, `memo-based-blockchains/`) keep their internal `01..NN` numbering.
