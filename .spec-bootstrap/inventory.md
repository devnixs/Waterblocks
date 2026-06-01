# Legacy Inventory Report — Waterblocks

## Repository Summary

- Application type: Fireblocks-API-compatible test double / admin tool for crypto-trading platform testing (with companion admin UI).
- Primary languages/frameworks: C# / ASP.NET Core 8 Web API + EF Core 10 (PostgreSQL); React 18 + TypeScript + Vite frontend; SignalR for realtime.
- Main entrypoints: `Waterblocks.Api/Program.cs` (backend), `waterblocks-admin/src/main.tsx` + `App.tsx` (frontend).
- Deployment/runtime shape: Docker Compose (`docker-compose*.yml`); CloudFormation ECS deploy (`cloudformation/waterblocks-ecs.yml`); CI via GitHub Actions.

## Existing Guidance

- Repo instructions: `CLAUDE.md`, `AGENTS.md`, `AGENTS_*.md` (API, CI, DOCKER, DOCS, TESTING, UI).
- README/docs: `README.md`, `SPECS.md`, swagger source-of-truth `Fireblocks.swagger.yml`.
- Existing specs/stories/issues: `stories/` directory present; no `specs/` yet.

## User-Facing Surfaces

- Routes/screens/pages: `/` (Transactions), `/transactions`, `/vaults`, `/workspaces`, `/assets`.
- Forms and major actions: create transaction, create vault, edit asset, bulk confirm transactions, manage workspaces, archive/unarchive vaults, transition transaction state, toggle auto-transitions.
- Navigation flows: app shell with nav links, workspace selector, keyboard shortcuts (1-4 + `?`), login gate.
- Error/empty/loading states: ToastProvider for global toasts; per-view loading via React Query; realtime status indicator.

## Backend/API Surfaces

- HTTP endpoints: 60+ across Fireblocks-compatible API (`/v1/...`) and Admin API (`/admin/...`); see `state.json` items 1-61.
- WebSocket/realtime endpoints: SignalR `AdminHub` with workspace-group broadcasts (`transactionUpserted`, `transactionsUpdated`, `vaultUpserted`, `vaultsUpdated`).
- Background jobs/queues: `AutoTransitionService` (BackgroundService) auto-advances transactions through state machine per-workspace.
- Integrations: Fireblocks API contract compatibility (no outbound integrations); SignalR for UI.
- Auth/authorization boundaries: `FireblocksAuthenticationMiddleware` (API key + JWT signing) for `/v1/*`; Admin API + SignalR hub unauthenticated (test-internal).

## Data And Persistence

- Databases: PostgreSQL via EF Core 10.
- Main entities/tables/collections: `Workspace`, `VaultAccount`, `Wallet`, `Address`, `Asset`, `Transaction` (+ `TransactionState` enum + state machine), `AdminSetting`, `ApiKey`.
- Migrations/schema files: 23 migrations under `Waterblocks.Api/Migrations/` (InitialCreate through AddTransactionInitiatedBy).
- File/object storage: none; seed data from `all_fireblocks_assets.json` / `all_assets.json`.
- Important config/env vars: `appsettings.json`, `appsettings.Development.json`; `.env` via dotenv.net.

## Current Tests And QA

- Unit tests: `tests/backend/unit/` and `tests/frontend/unit/` directories exist but are empty.
- Integration tests: 11 test files under `tests/backend/integration/` covering addresses, assets, transactions, balances, fees, vaults, workspaces.
- End-to-end tests: `tests/frontend/e2e/` directory empty (no Playwright/Cypress yet).
- Manual QA notes: `test.md`, `test-addresses.ps1`.
- CI jobs: `.github/workflows/ci.yml`, `.github/workflows/claude.yml`.

## Candidate Spec Areas

- Foundations: `specs/00-foundations.spec.md` — Program.cs wiring, middleware (error mapper, traffic logging), appsettings, toast/api client foundations, Vite config.
- Routing/navigation: `specs/01-routing-and-navigation.spec.md` — React Router routes, app shell, keyboard shortcuts, workspace selector.
- Auth/users: `specs/02-auth-and-identity.spec.md` — Fireblocks API key/JWT middleware; LoginGate; user identification on transactions.
- Core domain: `specs/03-core-workflows.spec.md` — transactions, vaults, wallets, addresses, assets, admin state transitions, auto-transition, fee estimation, address validation.
- Data/persistence: `specs/04-data-and-persistence.spec.md` — entities, DbContext, migrations, asset seed.
- Integrations: `specs/05-integrations.spec.md` — Fireblocks swagger contract, SignalR realtime channel.
- Testing/QA: `specs/06-testing-and-qa.spec.md` — integration tests + harness.
- Deployment/ops: `specs/07-deployment-and-ops.spec.md` — Dockerfiles, compose files, CloudFormation, CI workflows, run scripts.

## Discovered Behaviors

(Detailed behavior tables deferred to per-item sub-agent passes; see `state.json` for the full work queue of 158 items.)

## Ambiguities And Product Questions

- Question: Is the LoginGate intended as real authentication or purely a UX gate for capturing the operator email used in transaction attribution?
  - Evidence: `LoginGate.tsx` + `useCurrentUser.ts` only persist locally; backend Admin API has no auth.
  - Risk if guessed: Misdescribing security posture in specs.
  - Suggested owner: Product/security owner.
- Question: Is auto-transition per-workspace toggle intended for production-like flows or solely deterministic test scaffolding?
  - Evidence: `AutoTransitionService` runs continuously; `AdminSettingsController` exposes toggle.
  - Risk if guessed: Drift between spec intent and runtime defaults.
  - Suggested owner: Test platform owner.

## Suggested Bootstrap Stories

- Story: Backfill Fireblocks-compatible endpoint behavior specs against `Fireblocks.swagger.yml`.
  - Relevant proposed spec IDs: TXN-*, VAULT-*, ASSET-*.
  - Goal: Spec parity with upstream Fireblocks contract.
  - Verification: Integration tests under `tests/backend/integration/` pass; swagger diff clean.
- Story: Document Admin API state-machine workflows.
  - Relevant proposed spec IDs: ADM-TXN-*, TXN-* (state machine).
  - Goal: Deterministic test orchestration coverage.
  - Verification: New unit/integration tests around `TransactionStateMachine`.
- Story: Capture realtime SignalR channel contract.
  - Relevant proposed spec IDs: INT-WS-*.
  - Goal: Specify hub events consumed by UI.
  - Verification: `useRealtimeUpdates` continues to receive expected payloads.
