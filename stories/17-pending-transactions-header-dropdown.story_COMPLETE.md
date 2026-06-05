---
id: STORY-017
title: Pending transactions header dropdown across workspaces
target_specs:
  - specs/01-routing-and-navigation.spec.md
  - specs/03-core-workflows.spec.md
  - specs/05-integrations.spec.md
status: COMPLETE
---

## Goal

Add an app-shell pending-transactions header control that shows the realtime count of all non-terminal transactions across all workspaces and lets users jump straight from a dropdown summary row to the corresponding transaction detail view.

## Scope

- [X] Add an admin-wide pending-transactions summary API that returns only non-terminal transactions across all workspaces, includes the summary fields required by the header dropdown, and provides a workspace-scoped transaction id that can be used for direct navigation.
- [X] Extend the realtime contract so the pending-transactions header can refresh from websocket events when transactions are created or change state anywhere in the system, without depending on the currently selected workspace.
- [X] Add a header item labeled `{count} pending transactions` whose count reflects all non-terminal transactions across all workspaces.
- [X] Render the pending-transactions popup as a styled, scrollable `<div>` dropdown rather than a native `<select>`.
- [X] Show, for each dropdown row, the transaction amount, asset, status, source workspace, source address name, source address, destination workspace, destination address name, and destination address.
- [X] Clicking a dropdown row must close the dropdown, switch the active workspace context as needed, and navigate directly to `/transactions/:transactionId` with that transaction detail view open.
- [X] Check `UI-TXN-001`, `UI-SHELL-007`, `UI-SHELL-008`, `ADM-TXN-014`, `INT-WS-007`, and `INT-WS-008` from `- [ ]` to `- [X]` only after implementation and verification are complete.

## Relevant Specs

- [X] `specs/01-routing-and-navigation.spec.md` (`UI-TXN-001`, `UI-SHELL-007`, `UI-SHELL-008`)
- [X] `specs/03-core-workflows.spec.md` (`ADM-TXN-014`)
- [X] `specs/05-integrations.spec.md` (`INT-WS-007`, `INT-WS-008`)

## Acceptance Notes

- [X] The pending count must use the system's non-terminal transaction states only; terminal states (`COMPLETED`, `FAILED`, `REJECTED`, `CANCELLED`, `TIMEOUT`) must never be counted.
- [X] The count and dropdown scope all workspaces, not just the workspace selected in the header selector.
- [X] A transaction that touches two internal workspaces must appear once in the pending summary, not once per workspace.
- [X] The dropdown must remain small and internally scrollable when there are many pending transactions.
- [X] The dropdown UI must be a normal HTML container with CSS styling applied; do not implement this feature with a native `<select>`.
- [X] Clicking a pending-transaction row must preserve the normal transaction-detail actions after navigation.

## Playwright Test

- [X] Add or extend Playwright coverage in `e2e/tests/` for the header pending-transactions flow: seed at least two workspaces plus a mix of terminal and non-terminal transactions, open the admin UI, and assert that the header count matches only the non-terminal transactions across all workspaces.
- [X] Add a Playwright assertion that opening the header control reveals a scrollable dropdown container (not a native `<select>`) and that each row shows the documented summary fields for the pending transactions.
- [X] Add a Playwright assertion that clicking a dropdown row closes the dropdown, updates the active workspace selection as needed, navigates to `/transactions/:transactionId`, and shows the chosen transaction detail view.
- [X] Add a Playwright assertion that changing a transaction into or out of a non-terminal state updates the header count and dropdown contents through the realtime websocket flow without a manual page reload.
- [X] Run the targeted Playwright coverage and confirm it passes.

## Back-End Integration Test

- [X] Add backend integration coverage under `tests/backend/integration/` for `GET /admin/transactions/pending-summary`, asserting that it returns only non-terminal transactions, includes transactions from multiple workspaces, deduplicates cross-workspace internal transfers, and includes the documented workspace/address summary fields needed by the dropdown.
- [X] Add backend integration coverage for the admin-wide pending-transactions realtime event, asserting that the SignalR notification is emitted when a transaction is created or changes state in a way that affects the pending summary.
- [X] Run the targeted backend integration coverage and confirm it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm it passes with no regressions.

## Completion Rule

- [X] This story is complete only when `UI-TXN-001`, `UI-SHELL-007`, `UI-SHELL-008`, `ADM-TXN-014`, `INT-WS-007`, and `INT-WS-008` have been checked `- [X]`, the required Playwright coverage for the pending-transactions header flow has been added or extended and passes, the required back-end integration coverage for the pending summary API and realtime event has been added or extended and passes, and the full end-to-end test suite passes with no regressions.
