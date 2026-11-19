---
id: STORY-022
title: Allow one Gate account in each workspace
target_specs:
  - specs/03-core-workflows.spec.md
  - specs/04-data-and-persistence.spec.md
  - specs/06-testing-and-qa.spec.md
status: COMPLETE
---

## Goal

- [X] Match the Gate Off Exchange rule: a workspace has one Gate account, and one Gate account belongs to one workspace.

## Scope

- [X] Refuse a second `GATEIO` exchange account in a workspace, through the Fireblocks-compatible call and the admin call.
- [X] Refuse to connect one Gate simulator account in a second workspace.
- [X] Enforce both rules with filtered unique indexes, so that parallel calls cannot break them.
- [X] Show `A workspace can have only one Gate account.` in place of `Connect account` on the Exchanges page when the workspace has a Gate account.
- [X] Do not limit other exchange types, and do not add a delete call for exchange accounts.

## Relevant Specs

- [X] `specs/03-core-workflows.spec.md` (`EXCH-015`, `ADM-EXCH-003`, `UI-EXCH-001`)
- [X] `specs/04-data-and-persistence.spec.md` (`DATA-013`, `DATA-MIG-002`)
- [X] `specs/06-testing-and-qa.spec.md` (`TEST-016`, `TEST-026`)

## Acceptance Notes

- [X] A second `GATEIO` account in a workspace gets 409 with `GATE_ACCOUNT_ALREADY_EXISTS`.
- [X] A Gate simulator account that is connected in one workspace gets 409 with `EXCHANGE_ACCOUNT_ALREADY_CONNECTED` in another workspace.
- [X] Migration `OneGateAccountPerWorkspace` fails on a database that already breaks a rule. Repair such a database before the upgrade.

## Playwright Test

- [X] Extend `e2e/tests/off-exchange.spec.ts`: after a connection, the Exchanges page shows the one Gate account text and no `Connect account` button.
- [X] Run the targeted Playwright test and confirm that it passes.

## Back-End Integration Test

- [X] Add integration tests for both rules and for parallel creates in one workspace.
- [X] Run the full integration test project and confirm that it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm that it passes with no regressions.

## Workflow Validation

- [X] Run `npm run generate:story-map`.
- [X] Run `npm run validate:workflow`.

## Completion Rule

- [X] This story is complete only when the referenced spec IDs have been checked `- [X]`, the Playwright coverage has been added and passes, the back-end integration coverage has been added and passes, the full end-to-end suite passes with no regressions, and workflow validation succeeds.
