---
id: STORY-010
title: E2E tests for Admin UI workflows
target_specs:
  - specs/06-testing-and-qa.spec.md
  - specs/01-routing-and-navigation.spec.md
  - specs/03-core-workflows.spec.md
status: TODO
---

## Goal

The repository has no automated coverage for the React Admin UI. All `UI-*` checklist items in `specs/01-routing-and-navigation.spec.md` and `specs/03-core-workflows.spec.md` (UI-TXN-001..008, UI-VAULT-001..010, UI-WS-001, UI-ASSET-001..002, UI-SHELL-001..006) and the `AUTH-UI-*` items in `specs/02-auth-and-identity.spec.md` are implemented but unverified. Add a Playwright e2e suite that exercises the high-traffic admin journeys end-to-end against a running stack, and register a new `TEST-*` checklist item in `specs/06-testing-and-qa.spec.md` describing the suite.

## Scope

- Frontend e2e harness (Playwright) wired into `docker-compose.full.yml`.
- Smoke flows: login (LoginGate), create vault, create wallet, create transaction (each endpoint mode), drive a transaction to COMPLETED via admin actions, archive/unarchive vault, create/edit asset.
- Validate realtime updates appear without page reload (covers `INT-WS-006`).

## Relevant Specs

- `specs/01-routing-and-navigation.spec.md` (UI-SHELL-001..006, UI-TXN-001, UI-VAULT-001, UI-WS-001, UI-ASSET-001)
- `specs/03-core-workflows.spec.md` (UI-TXN-001..008, UI-VAULT-001..010, UI-ASSET-002)
- `specs/02-auth-and-identity.spec.md` (AUTH-UI-001..003)
- `specs/05-integrations.spec.md` (INT-WS-006)
- `specs/06-testing-and-qa.spec.md` (new TEST-* entry)

## Acceptance Notes

- Suite runs in CI against a docker-compose-managed stack.
- Each high-value `UI-*` item above has at least one assertion that touches it.

## Acceptance Criteria

- [ ] Playwright project scaffolded under `tests/frontend/e2e/`.
- [ ] Smoke spec covering create-transaction happy path and bulk-action selection.
- [ ] Vault lifecycle spec (create, archive, unarchive) verifies hidden-by-default + includeArchived behavior in the UI.
- [ ] Realtime update assertion (admin action triggers a row change without manual reload).
- [ ] `specs/06-testing-and-qa.spec.md` gains a new `TEST-NNN` checklist item describing the suite and references the relevant `UI-*` IDs.

## Playwright Test

See acceptance criteria — Playwright project is the primary deliverable.

## Browser Test

Run `npx playwright test` against a local `docker-compose.full.yml` stack and confirm green.

## E2E Regression

This story IS the e2e regression baseline for the UI.

## Completion Rule

Story is complete when Playwright suite is green in CI, the new `TEST-*` checklist item in `specs/06-testing-and-qa.spec.md` is checked, and the referenced `UI-*` items remain checked.
