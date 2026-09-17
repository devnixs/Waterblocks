---
id: STORY-019
title: Simulate source-less BTC exchange deposits
target_specs:
  - specs/03-core-workflows.spec.md
  - specs/04-data-and-persistence.spec.md
  - specs/05-integrations.spec.md
  - specs/06-testing-and-qa.spec.md
status: COMPLETE
---

## Goal

- [X] Let testers create BTC deposits from Coinbase or another exchange when Fireblocks cannot provide a source address, and return the same source-less transaction shape observed in production when the transaction is polled.

## Scope

- [X] Add explicit persisted source-less transaction metadata plus nullable Fireblocks output index, block height, and block hash fields and the corresponding EF Core migration.
- [X] Extend the Admin transaction creation contract and validation so only BTC-family incoming transactions to an internally owned destination can be source-less.
- [X] Preserve an empty source address, use the existing incoming balance-credit flow, and keep ordinary vault and one-time-address transaction behavior unchanged.
- [X] Extend Fireblocks-compatible transaction mapping so an explicitly source-less transaction returns `source: { id: "", type: "UNKNOWN", name: "External", subType: "" }`, `sourceAddress: ""`, and its persisted `index` and `blockInfo`.
- [X] Ensure the compatible response also matches the observed completed BTC deposit fields for hash, destination vault/address, confirmations, amounts, zero network fee when configured, `TRANSFER` operation, and `BASE_ASSET` asset type.
- [X] Add a BTC-only Coinbase/exchange source option to the Admin UI, require a destination vault, collect output index and completed block metadata, and clearly label source-less transactions in list/detail views.
- [X] Check `TXN-025`, `ADM-TXN-015`, `UI-TXN-010`, `DATA-012`, `INT-002`, and `TEST-014` from `- [ ]` to `- [X]` only after implementation and verification are complete.

## Relevant Specs

- [X] `specs/03-core-workflows.spec.md` (`TXN-025`, `ADM-TXN-015`, `UI-TXN-010`)
- [X] `specs/04-data-and-persistence.spec.md` (`DATA-012`)
- [X] `specs/05-integrations.spec.md` (`INT-002`)
- [X] `specs/06-testing-and-qa.spec.md` (`TEST-014`)

## Acceptance Notes

- [X] Treat “Coinbase” as an external exchange-originated BTC deposit whose UTXO source cannot be represented, not as a Bitcoin mining coinbase transaction.
- [X] Match the production Fireblocks source contract exactly: empty source id, `UNKNOWN` type, `External` name, empty subType, and empty sourceAddress.
- [X] Persist and return the Fireblocks `index` because consumers use it as the transaction output ordinal when one hash produces multiple rows.
- [X] Never generate or expose a synthetic source address for this simulation.
- [X] Keep the special mapping explicit so an ordinary external transaction with a real source address continues to return `ONE_TIME_ADDRESS`.
- [X] Validate user input at the Admin API boundary and do not accept source-less simulations for non-BTC assets or destinations outside a Waterblocks vault.

## Playwright Test

- [X] Add or extend coverage under `e2e/tests/` to select BTC, choose the Coinbase/exchange source, choose a destination vault, enter amount/hash/index/block metadata, submit, and assert the resulting row/detail visibly identifies a source-less exchange deposit without a generated source address.
- [X] Assert the source-less option is unavailable for a non-BTC asset and that a destination vault is required.
- [X] Run the targeted Playwright test and confirm it passes.

## Back-End Integration Test

- [X] Add a source-less BTC deposit integration test under `tests/backend/integration/` that creates the transaction through `POST /admin/transactions`, verifies the destination balance is credited once, then polls `GET /v1/transactions/{id}` and asserts the complete observed Fireblocks source peer, empty sourceAddress, output index, blockInfo, confirmations, hash, destination metadata, amountInfo, feeInfo/networkFee, operation, and asset type.
- [X] Extend the integration Fireblocks response test DTOs only as needed to deserialize and assert the full contract.
- [X] Add negative and non-regression cases proving unsupported source-less requests are rejected and normal external-source transactions still return `ONE_TIME_ADDRESS` with their address.
- [X] Run the targeted back-end integration tests and confirm they pass.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm it passes with no regressions.

## Workflow Validation

- [X] Run `npm run generate:story-map`.
- [X] Run `npm run validate:workflow`.

## Completion Rule

- [X] This story is complete only when `TXN-025`, `ADM-TXN-015`, `UI-TXN-010`, `DATA-012`, `INT-002`, and `TEST-014` have been checked `- [X]`, the required Playwright coverage for the source-less BTC Admin UI flow has been added and passes, the required back-end integration coverage for the Admin and Fireblocks-compatible contracts has been added and passes, the full end-to-end suite passes with no regressions, and workflow validation succeeds.
