# Product Vision

Waterblocks is a local, deterministic replacement for the Fireblocks API used by teams that need to test crypto trading and treasury workflows without touching real blockchains or live Fireblocks accounts.

## Purpose

The product simulates the Fireblocks API closely enough that an application can point its test environment at Waterblocks and exercise vault, wallet, address, asset, fee, and transaction flows end to end. It also provides an internal Admin API and Admin UI so testers can inspect state, create scenarios, and force transaction lifecycle outcomes.

## Target Users

- Developers integrating applications with Fireblocks-compatible APIs.
- QA engineers building deterministic end-to-end test suites for crypto workflows.
- Support and operations engineers who need a safe sandbox for transaction-state debugging.

## Core Workflows

- Create and manage workspaces, API keys, vault accounts, wallets, and asset metadata.
- Submit Fireblocks-compatible transactions and query them by id, external id, hash, status, or time range.
- Drive transactions through success, failure, cancellation, rejection, timeout, freeze, unfreeze, and drop flows.
- Observe and manipulate test data through the Admin UI, including realtime SignalR updates.
- Run automated integration and browser tests against the real ASP.NET Core API and PostgreSQL database.

## Boundaries

- Waterblocks is for deterministic testing, not custody, signing, settlement, or production blockchain interaction.
- Admin/Test APIs intentionally have relaxed authentication for internal test environments.
- Fireblocks-compatible endpoints should preserve the external contract from `Fireblocks.swagger.yml`; behavior that intentionally diverges must be captured in specs and stories.
