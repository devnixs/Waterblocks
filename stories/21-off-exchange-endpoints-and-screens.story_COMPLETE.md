---
id: STORY-021
title: Add Off Exchange endpoints and admin screens
target_specs:
  - specs/00-foundations.spec.md
  - specs/01-routing-and-navigation.spec.md
  - specs/02-auth-and-identity.spec.md
  - specs/03-core-workflows.spec.md
  - specs/04-data-and-persistence.spec.md
  - specs/05-integrations.spec.md
  - specs/06-testing-and-qa.spec.md
  - specs/07-deployment-and-ops.spec.md
status: COMPLETE
---

## Goal

- [X] Let testers and the LL engine use Fireblocks-compatible exchange accounts, Add collateral and Remove collateral on Waterblocks.
- [X] Let testers connect a Gate simulator account in the admin UI, and run Add collateral and Remove collateral from there.

## Scope

- [X] Add the Fireblocks-compatible `/exchange_accounts` and `/off_exchange` endpoints that the specs list.
- [X] Add the `/admin/exchange-accounts` endpoints.
- [X] Store exchange accounts, collateral balances, mirror events and idempotency records in PostgreSQL.
- [X] Mirror collateral changes to a connected Gate simulator account with one background worker.
- [X] Release funds on every final state other than `COMPLETED`, `POST /transactions/{txId}/cancel` included.
- [X] Add the Exchanges page, the connect dialog, the CVA screen, the collateral transfer dialog and the transfers list.
- [X] Do not add settlement, deposit to exchange, withdraw from exchange, sub account screens, other exchange providers in the UI, USD values, new transaction states, webhooks or JWT signature checks.

## Relevant Specs

- [X] `specs/00-foundations.spec.md` (`FND-006`, `FND-007`)
- [X] `specs/01-routing-and-navigation.spec.md` (`RTE-001`, `UI-SHELL-009`, `UI-SHELL-010`)
- [X] `specs/02-auth-and-identity.spec.md` (`AUTH-002`)
- [X] `specs/03-core-workflows.spec.md` (`TXN-017`, `TXN-026`, `TXN-027`, `EXCH-001` to `EXCH-014`, `ADM-EXCH-001` to `ADM-EXCH-007`, `UI-EXCH-001` to `UI-EXCH-007`, `UI-TXN-011`)
- [X] `specs/04-data-and-persistence.spec.md` (`DATA-013` to `DATA-017`, `DATA-MIG-001`)
- [X] `specs/05-integrations.spec.md` (`INT-003` to `INT-007`)
- [X] `specs/06-testing-and-qa.spec.md` (`TEST-015` to `TEST-025`)
- [X] `specs/07-deployment-and-ops.spec.md` (`OPS-012`)

## Acceptance Notes

- [X] The admin collateral calls and the Fireblocks-compatible calls use the same service code.
- [X] All balance effects go through `ReserveFundsAsync`, `CompleteTransactionAsync` and `RollbackTransactionAsync`.
- [X] The collateral vault address is not stored in `Addresses`.
- [X] `POST /transactions` keeps its behaviour for vault and one-time address peers, and does not read `Idempotency-Key`.
- [X] A duplicate `externalTxId` keeps the existing Waterblocks reply: HTTP 409 with code 1438.

## Playwright Test

- [X] Add `e2e/fake-gate-simulator.mjs` and start it from the Playwright configuration.
- [X] Add `e2e/tests/off-exchange.spec.ts` for the empty state, connect, a wrong key message, the CVA screen, Add collateral, Remove collateral, a rejected removal and the Transactions page labels.
- [X] Run the targeted Playwright test and confirm that it passes.

## Back-End Integration Test

- [X] Add integration tests under `tests/backend/integration/` for `TEST-015` to `TEST-024`, with a test double for the Gate simulator.
- [X] Run the full integration test project and confirm that it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm that it passes with no regressions.

## Workflow Validation

- [X] Run `npm run generate:story-map`.
- [X] Run `npm run validate:workflow`.

## Completion Rule

- [X] This story is complete only when the referenced spec IDs have been checked `- [X]`, the Playwright coverage has been added and passes, the back-end integration coverage has been added and passes, the full end-to-end suite passes with no regressions, and workflow validation succeeds.
