---
id: STORY-023
title: Make the exchange code generic with one simulator client for each exchange
target_specs:
  - specs/00-foundations.spec.md
  - specs/03-core-workflows.spec.md
  - specs/04-data-and-persistence.spec.md
  - specs/05-integrations.spec.md
  - specs/06-testing-and-qa.spec.md
  - specs/07-deployment-and-ops.spec.md
status: COMPLETE
---

## Goal

- [X] Keep the Off Exchange tables, the API, the settings and the Admin UI free of exchange names, as in the Fireblocks interfaces.
- [X] Let a new exchange (for example Bybit, Crypto.com or Deribit) connect through one new simulator client, with no change to the tables, the API or the Admin UI.

## Scope

- [X] Add `IExchangeSimulatorClient`, `ExchangeSimulatorClientBase`, `IExchangeSimulatorRegistry` and `AddExchangeSimulatorClient<T>()` in `Waterblocks.Api/Services/ExchangeSimulators`.
- [X] Move the Gate simulator calls into `GateSimulatorClient`, the first client. It sets the exchange type `GATEIO`, the name `Gate.io`, the assets and the one-account rule.
- [X] Rename the connection columns to `SimulatorAccountId` and `SimulatorAccountName`, add `ExchangeType` to the mirror events, and use generic index names.
- [X] Replace the two migrations of this branch with one migration, `AddOffExchangeCollateral`. The migrations of `main` do not change.
- [X] Use generic error codes: `EXCHANGE_SIMULATOR_NOT_CONFIGURED`, `EXCHANGE_SIMULATOR_UNREACHABLE`, `SIMULATOR_ACCOUNT_NOT_FOUND` and `EXCHANGE_ACCOUNT_LIMIT_REACHED`.
- [X] Use generic settings: `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__BASE_URL` and `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__ADMIN_TOKEN`.
- [X] Add GET /admin/exchange-providers. The Admin UI reads the provider list, the exchange names, the assets and the one-account rule from it.
- [X] Accept the exchange type of each simulator client in POST /exchange_accounts, also when the Fireblocks list of that call does not have it (for example `DERIBIT`).
- [X] Remove exchange names from the table headers, labels and messages of the Exchanges screens.
- [X] Do not add a second real exchange client. Do not change the Fireblocks-compatible contract.

## Relevant Specs

- [X] `specs/00-foundations.spec.md` (`FND-006`)
- [X] `specs/03-core-workflows.spec.md` (`EXCH-001`, `EXCH-004`, `EXCH-005`, `EXCH-012`, `EXCH-015`, `ADM-EXCH-002`, `ADM-EXCH-003`, `ADM-EXCH-006`, `ADM-EXCH-008`, `UI-EXCH-001`, `UI-EXCH-002`, `UI-EXCH-003`, `UI-EXCH-005`)
- [X] `specs/04-data-and-persistence.spec.md` (`DATA-013`, `DATA-015`, `DATA-MIG-001`)
- [X] `specs/05-integrations.spec.md` (`INT-004`, `INT-005`, `INT-006`, `INT-007`)
- [X] `specs/06-testing-and-qa.spec.md` (`TEST-016`, `TEST-026`, `TEST-027`)
- [X] `specs/07-deployment-and-ops.spec.md` (`OPS-012`)

## Acceptance Notes

- [X] No table, column or index name contains an exchange name.
- [X] The only exchange-specific code is `GateSimulatorClient` and its registration line in `Program.cs`.
- [X] A database that applied the two earlier migrations of this branch must be recreated. No shared database applied them.

## Playwright Test

- [X] Update `e2e/tests/off-exchange.spec.ts`: the connect dialog lists the providers of GET /admin/exchange-providers, the Exchanges table headers name no exchange, and the texts use the exchange name from the API.
- [X] Run the targeted Playwright test and confirm that it passes.

## Back-End Integration Test

- [X] Add `OffExchangeProviderTests` with test-only simulator clients for `BYBIT_V2` and `DERIBIT`.
- [X] Update the existing Off Exchange tests to the generic names and codes.
- [X] Run the full integration test project and confirm that it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm that it passes with no regressions.

## Workflow Validation

- [X] Run `npm run generate:story-map`.
- [X] Run `npm run validate:workflow`.

## Completion Rule

- [X] This story is complete only when the referenced spec IDs have been checked `- [X]`, the Playwright coverage has been added and passes, the back-end integration coverage has been added and passes, the full end-to-end suite passes with no regressions, and workflow validation succeeds.
