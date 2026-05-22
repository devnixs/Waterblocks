---
id: STORY-014
title: Remove or wire up unused FireblocksAssetSeed DTO
target_specs:
  - specs/04-data-and-persistence.spec.md
status: TODO
---

## Goal

`DATA-011` (item ITEM-0158) flagged `FireblocksAssetSeed` as "unused by active seeder". Either delete the dead DTO + `all_fireblocks_assets.json` resource or wire it into `SeedData` so the codebase matches its specs. The current state risks future contributors picking the wrong seed path.

## Scope

- Decide: keep `FireblocksAssetSeed` (then connect to `SeedData.SeedAssets`) or remove it together with `Waterblocks.Api/all_fireblocks_assets.json`.
- Update `DATA-010` and `DATA-011` checklist text accordingly.

## Relevant Specs

- `specs/04-data-and-persistence.spec.md` (DATA-010, DATA-011)

## Acceptance Notes

- No behavior change for end users; this is a code hygiene story.

## Acceptance Criteria

- [ ] Either path implemented (remove or wire up).
- [ ] Specs updated so they describe only the live seed source.

## Playwright Test

N/A.

## Browser Test

N/A.

## E2E Regression

Existing seed-dependent tests must still pass.

## Completion Rule

Code matches spec. No dead seed plumbing.
