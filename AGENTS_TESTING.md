# AGENTS_TESTING.md

Date: 2026-01-11

Work summary:
- Added tests directory scaffolding under `tests/` with placeholder files.

Files touched:
- `tests/backend/unit/.gitkeep`
- `tests/backend/integration/.gitkeep`
- `tests/frontend/unit/.gitkeep`
- `tests/frontend/e2e/.gitkeep`

Date: 2026-11-19

Work summary (STORY-021):
- Added integration tests for exchange accounts, collateral transfers, the collateral mirror and `Idempotency-Key`.
- `FakeExchangeSimulator` is the test double of an exchange simulator. `FakeSimulatorNetwork` routes each simulator URL to its fake, and `OffExchangeTestBase` installs it as the primary handler of the typed HTTP client of `ExchangeSimulatorClient`.
- `CapturingLogSink` lets tests search the API logs for credentials.
- Added `e2e/fake-exchange-simulator.mjs` as a Playwright web server, so that browser tests need no real simulator. The API server entry receives its URL.
- Added `e2e/tests/off-exchange.spec.ts`.

Files touched:
- `tests/backend/integration/OffExchangeAccountTests.cs`
- `tests/backend/integration/OffExchangeCollateralTests.cs`
- `tests/backend/integration/OffExchangeMirrorTests.cs`
- `tests/backend/integration/OffExchangeIdempotencyTests.cs`
- `tests/backend/integration/Infrastructure/`
- `e2e/fake-exchange-simulator.mjs`, `e2e/playwright.config.ts`, `e2e/tests/off-exchange.spec.ts`

Work summary (STORY-022):
- Added integration tests for the one-account rule with Gate.io, one connection for each simulator account in all workspaces, and parallel creates in one workspace.
- The paging test now uses other exchange types for its extra accounts.
- The e2e connect test checks the one-account text on the Exchanges page.

Files touched:
- `tests/backend/integration/OffExchangeAccountTests.cs`
- `e2e/tests/off-exchange.spec.ts`

Work summary (STORY-023):
- Added `ExchangeCatalogTests`. They onboard exchanges through the admin API only: a new `DERIBIT` exchange and the seeded `BYBIT_V2` connect, get their own mirror events, use their own currencies and rules, and need no code. They also check the catalog validation and the seeded Gate.io facts.
- Added credentials tests: a real RSA public key, `POST /exchange_accounts` with `key` and `creds`, refused creds (not decryptable, other tenant, other key, wrong secret) and no plain secret on the admin calls.
- `FakeExchangeSimulator` checks the HMAC key proof and refuses a body that holds a secret. `OffExchangeTestBase.EncryptCredsAsync` encrypts creds in the Fireblocks way.
- Added tests for the Gate.io currencies: a `USDT_ERC20` add reaches the fake simulator as `USDT`, and its `USDT` balance shows on `USDT_ERC20`.
- The e2e tests use `e2e/fake-exchange-simulator.mjs`. A new e2e test adds an exchange on the Supported exchanges page, connects an account to it and moves collateral. `E2E_SIMULATOR_URL_FOR_API` gives the fake simulator address as the API sees it.

Files touched:
- `tests/backend/integration/ExchangeCatalogTests.cs`
- `tests/backend/integration/Infrastructure/FakeExchangeSimulator.cs`, `OffExchangeTestBase.cs`
- `tests/backend/integration/OffExchangeAccountTests.cs`, `OffExchangeCollateralTests.cs`, `OffExchangeMirrorTests.cs`
- `e2e/fake-exchange-simulator.mjs`, `e2e/playwright.config.ts`, `e2e/tests/off-exchange.spec.ts`
