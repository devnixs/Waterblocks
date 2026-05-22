# Spec Index

This index lists every spec file in this repository. Each entry shows its area, owners, ID prefix, and a short blurb summarising the surface it governs. Specs are grouped by domain.

## Foundations

### specs/00-foundations.spec.md

- Area: `foundations`
- Owners: `backend`
- ID prefix: `FND` (with `UI-FND` for frontend foundations)
- Blurb: Health endpoint, default ASP.NET configuration (appsettings, Serilog, Datadog), Program.cs DI/middleware/Hosted-service composition, the Fireblocks error-mapper and HTTP traffic-logging middleware, and frontend platform foundations (ToastProvider, Admin API client/React Query hooks, Vite/React tooling and pinned deps).

## Routing & Shell

### specs/01-routing-and-navigation.spec.md

- Area: `routing`
- Owners: `frontend`
- ID prefix: `RTE` (with `UI-TXN`, `UI-VAULT`, `UI-WS`, `UI-ASSET`, `UI-SHELL`)
- Blurb: SPA route map (`/`, `/transactions`, `/vaults`, `/workspaces`, `/assets`), App shell behaviors (header, workspace selector, auto-transition toggle, realtime status, login gating, keyboard shortcuts), and the `SearchableVaultSelect` component.

## Auth & Identity

### specs/02-auth-and-identity.spec.md

- Area: `auth`
- Owners: `frontend`
- ID prefix: `AUTH` (with `AUTH-UI`)
- Blurb: Fireblocks-style request authentication middleware (X-API-Key/Bearer/workspace context), anonymous endpoint allowlist, the client-side `LoginGate` and `useCurrentUser` hook, and `initiatedBy` identity attribution on transactions.

## Core Workflows (Fireblocks API, Admin API, UI workflows)

### specs/03-core-workflows.spec.md

- Area: `core-workflows`
- Owners: `backend`
- ID prefix: `TXN`, `VAULT`, `ASSET`, `ADM-ADDR`, `ADM-ASSET`, `ADM-SET`, `ADM-TXN`, `ADM-VAULT`, `ADM-WS` (server side); `UI-TXN`, `UI-VAULT`, `UI-ASSET` (client side)
- Blurb: All Fireblocks-compatible endpoints (assets, network fees, transactions, vault accounts/wallets/addresses, unspent inputs); the Admin API surface (assets, settings, transactions including explicit state-transition endpoints, vaults, workspaces); the transaction state machine and auto-transition background job; transaction/vault/asset UI forms, tables, headers, pagers, detail panels, and dialogs.

## Data & Persistence

### specs/04-data-and-persistence.spec.md

- Area: `data`
- Owners: `backend`
- ID prefix: `DATA` (with `DATA-MIG` for migrations)
- Blurb: EF Core entities (Workspace, ApiKey, VaultAccount, Wallet, Address, Asset, Transaction, TransactionState, AdminSetting), seed data behavior, and the chronological migration history that establishes multi-tenancy, soft-delete/archive, fee config, blockchain types, identity attribution, and case sensitivity.

## Integrations

### specs/05-integrations.spec.md

- Area: `integrations`
- Owners: `backend`
- ID prefix: `INT` (with `INT-WS` for SignalR realtime)
- Blurb: The Fireblocks OpenAPI contract that the `/v1/*` surface mirrors, plus the SignalR `/hubs/admin` realtime channel — workspace-group join/leave, `transactionUpserted`/`transactionsUpdated`/`vaultUpserted`/`vaultsUpdated` events, and the client-side `useRealtimeUpdates` reconnect/cache-invalidation hook.

## Testing & QA

### specs/06-testing-and-qa.spec.md

- Area: `testing-and-qa`
- Owners: `qa`, `backend`
- ID prefix: `TEST`
- Blurb: Integration test harness (per-test isolated Postgres + `WebApplicationFactory<Program>` + workspace/API-key bootstrap) and the integration tests covering address case sensitivity, address generation, admin asset/transaction CRUD and validation, balance tracking, cross-asset address resolution, fee handling, vault archiving, wallet address sharing, and workspace isolation.

## Deployment & Ops

### specs/07-deployment-and-ops.spec.md

- Area: `deployment-and-ops`
- Owners: `devops`
- ID prefix: `OPS`
- Blurb: Docker Compose stacks (default, backend-only, frontend-only, full), backend and frontend Dockerfiles + nginx config + runtime config templating, the CloudFormation ECS/RDS/ALB deployment template, GitHub Actions CI (build/test + multi-arch image publish), the Claude workflow, and the run-compose dispatch scripts.
