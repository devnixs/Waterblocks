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
- `FakeGateSimulator` is the test double for the Gate simulator. `OffExchangeTestBase` installs it as the primary handler of the typed HTTP client of `GateSimulatorClient`.
- `CapturingLogSink` lets tests search the API logs for credentials.
- Added `e2e/fake-gate-simulator.mjs` as a Playwright web server, so that browser tests need no real simulator. The API server entry receives its URL.
- Added `e2e/tests/off-exchange.spec.ts`.

Files touched:
- `tests/backend/integration/OffExchangeAccountTests.cs`
- `tests/backend/integration/OffExchangeCollateralTests.cs`
- `tests/backend/integration/OffExchangeMirrorTests.cs`
- `tests/backend/integration/OffExchangeIdempotencyTests.cs`
- `tests/backend/integration/Infrastructure/`
- `e2e/fake-gate-simulator.mjs`, `e2e/playwright.config.ts`, `e2e/tests/off-exchange.spec.ts`

Work summary (STORY-022):
- Added integration tests for the one-account rule with Gate.io, one connection for each simulator account in all workspaces, and parallel creates in one workspace.
- The paging test now uses other exchange types for its extra accounts.
- The e2e connect test checks the one-account text on the Exchanges page.

Files touched:
- `tests/backend/integration/OffExchangeAccountTests.cs`
- `e2e/tests/off-exchange.spec.ts`

Work summary (STORY-023):
- Added `OffExchangeProviderTests`. It adds `FakeExchangeSimulatorClient`, an in-memory simulator client, for `BYBIT_V2` and `DERIBIT` next to the Gate.io client. The tests prove that a new exchange needs only a new client: the provider list, the exchange type check, the connection, the mirror events, the asset check, the one-account rule and the connection index work for each exchange.
- `OffExchangeTestBase.ConfigureServices` is virtual, so that a test class can add a client.
- Updated the existing Off Exchange tests to the generic names and error codes.
- The e2e test checks the provider list of the connect dialog against `GET /admin/exchange-providers`, and checks that the Exchanges table headers name no exchange.

Files touched:
- `tests/backend/integration/OffExchangeProviderTests.cs`
- `tests/backend/integration/Infrastructure/FakeExchangeSimulatorClient.cs`, `OffExchangeTestBase.cs`
- `tests/backend/integration/OffExchangeAccountTests.cs`, `OffExchangeCollateralTests.cs`
- `e2e/playwright.config.ts`, `e2e/tests/off-exchange.spec.ts`
