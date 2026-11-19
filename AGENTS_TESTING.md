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
- Added integration tests for exchange accounts, collateral transfers, the Gate mirror and `Idempotency-Key`.
- `FakeGateSimulator` is the test double for the Gate simulator. `OffExchangeTestBase` installs it as the primary handler of the typed HTTP client.
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
