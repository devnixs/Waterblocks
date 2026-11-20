---
id: STORY-023
title: Keep exchanges as data in an exchange catalog, with one simulator client and encrypted credentials
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

- [X] Keep the Off Exchange tables, the API, the settings, the screens and the code files free of exchange names, as in the Fireblocks interfaces.
- [X] Onboard a new exchange (for example Bybit, Crypto.com or Deribit) with data only: no code change and no PR.
- [X] Handle exchange credentials as Fireblocks does, so that the secret never travels in clear text.

## Scope

- [X] Add the exchange catalog: the `Exchanges` and `ExchangeCurrencies` tables, the startup seed `all_exchanges.json` (the exchange types of the Fireblocks "Add an exchange account" call), and `GET`, `POST` and `PUT /admin/exchanges`.
- [X] Move every exchange fact into the catalog: the name, the simulator URL, the one-account rule and the currency names (Gate.io: `BTC` = `BTC`, `USDT_ERC20` = `USDT`). Remove the hard-coded exchange list and the exchange-specific client class.
- [X] Use one generic `ExchangeSimulatorClient` for the simulator admin API of every exchange: `POST /_api/keys/verify`, `GET /_api/accounts/{name}` and `POST /_api/collateral/events`.
- [X] Check an API key with an HMAC-SHA256 key proof, so that the secret never reaches the simulator.
- [X] Encrypt credentials as Fireblocks does: `creds` is the RSA-OAEP SHA-256 encryption of `{"apiKey","secret","tenantId"}` with a real credentials public key. `POST /exchange_accounts` with `key` and `creds` connects the account. The admin calls accept only `creds`, and the Admin UI encrypts in the browser.
- [X] Rename the connection columns to `SimulatorAccountId` and `SimulatorAccountName`, add `ExchangeType` to the mirror events, and use generic index names and error codes. The catalog tables are in the one migration of this branch, `AddOffExchangeCollateral` (STORY-024 merged the migrations).
- [X] Add the Supported exchanges page, and remove exchange names from the table headers, labels and messages of the Exchanges screens.
- [X] Do not change the Fireblocks-compatible replies of the other calls.

## Relevant Specs

- [X] `specs/00-foundations.spec.md` (`FND-006`)
- [X] `specs/03-core-workflows.spec.md` (`EXCH-001`, `EXCH-003`, `EXCH-004`, `EXCH-005`, `EXCH-012`, `EXCH-015`, `EXCH-016`, `ADM-EXCH-002`, `ADM-EXCH-003`, `ADM-EXCH-004`, `ADM-EXCH-006`, `ADM-EXCH-008`, `ADM-EXCH-009`, `UI-EXCH-001`, `UI-EXCH-002`, `UI-EXCH-003`, `UI-EXCH-005`, `UI-EXCH-008`)
- [X] `specs/04-data-and-persistence.spec.md` (`DATA-013`, `DATA-015`, `DATA-018`, `DATA-019`, `DATA-020`, `DATA-MIG-001`)
- [X] `specs/05-integrations.spec.md` (`INT-004`, `INT-005`, `INT-006`, `INT-007`, `INT-008`)
- [X] `specs/06-testing-and-qa.spec.md` (`TEST-016`, `TEST-017`, `TEST-025`, `TEST-026`, `TEST-027`, `TEST-028`, `TEST-029`)
- [X] `specs/07-deployment-and-ops.spec.md` (`OPS-012`)

## Acceptance Notes

- [X] No table, column, index, code file or class name contains an exchange name. Exchange names exist only as catalog data and in the seed file.
- [X] A database that applied an earlier migration of this branch must be recreated. No shared database applied them.
- [X] An exchange simulator must serve `POST /_api/keys/verify`. The Gate simulator gets this call in its own repository.

## Playwright Test

- [X] Update `e2e/tests/off-exchange.spec.ts`: the connect dialog lists the connectable exchanges of the catalog, the Exchanges table headers name no exchange, and a new test adds an exchange on the Supported exchanges page, connects an account to it and moves collateral.
- [X] Run the targeted Playwright test and confirm that it passes.

## Back-End Integration Test

- [X] Add `ExchangeCatalogTests`, credentials tests and the generic `FakeExchangeSimulator`.
- [X] Update the existing Off Exchange tests to the generic names, codes and encrypted credentials.
- [X] Run the full integration test project and confirm that it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm that it passes with no regressions.

## Workflow Validation

- [X] Run `npm run generate:story-map`.
- [X] Run `npm run validate:workflow`.

## Completion Rule

- [X] This story is complete only when the referenced spec IDs have been checked `- [X]`, the Playwright coverage has been added and passes, the back-end integration coverage has been added and passes, the full end-to-end suite passes with no regressions, and workflow validation succeeds.
