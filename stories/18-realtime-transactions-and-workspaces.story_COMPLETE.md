---
id: STORY-018
title: Realtime transactions and workspace lists
target_specs:
  - specs/01-routing-and-navigation.spec.md
  - specs/03-core-workflows.spec.md
  - specs/05-integrations.spec.md
status: COMPLETE
---

## Goal

Make the admin UI reflect new transactions and workspace-list changes live over SignalR so users do not need to manually refresh the page.

## Scope

- [X] Fan out transaction realtime events to every workspace that can observe the transaction, not only the workspace that initiated the mutation.
- [X] Keep the selected-workspace transactions page live so new visible transactions appear automatically and an open transaction detail panel refreshes in place when the transaction changes.
- [X] Add an admin-wide workspace-list realtime event so the shell workspace dropdown and `/workspaces` page stay synchronized when workspaces are created or archived.
- [X] Preserve the existing pending-transactions header realtime behavior while extending the shared realtime hook for the new workspace-list event.
- [X] Check `UI-SHELL-001`, `UI-WS-001`, `UI-TXN-009`, `INT-WS-001`, `INT-WS-002`, `INT-WS-003`, `INT-WS-005`, `INT-WS-006`, and `INT-WS-009` from `- [ ]` to `- [X]` only after implementation and verification are complete.

## Relevant Specs

- [X] `specs/01-routing-and-navigation.spec.md` (`UI-SHELL-001`, `UI-WS-001`)
- [X] `specs/03-core-workflows.spec.md` (`UI-TXN-009`)
- [X] `specs/05-integrations.spec.md` (`INT-WS-001`, `INT-WS-002`, `INT-WS-003`, `INT-WS-005`, `INT-WS-006`, `INT-WS-009`)

## Acceptance Notes

- [X] Cross-workspace internal transfers must trigger live transaction-list updates in both the source workspace and the destination workspace.
- [X] A newly created transaction that becomes visible in the currently selected workspace must appear on `TransactionsPage` without a manual reload.
- [X] The workspace selector in the app shell must add new workspaces and remove archived workspaces live without requiring route navigation or a full page refresh.
- [X] The `/workspaces` page must add or remove cards live when the visible workspace list changes elsewhere.
- [X] Existing pending-transactions dropdown behavior must continue to update in realtime.

## Playwright Test

- [X] Extend Playwright coverage in `e2e/tests/pending-transactions.spec.ts` or a nearby realtime-focused spec to prove that a transaction created for a second workspace appears in that workspace's transactions list without manually reloading the page.
- [X] Extend Playwright coverage in `e2e/tests/workspaces.spec.ts` to prove that creating or archiving a workspace through the API updates both the shell workspace selector and the `/workspaces` card list live.
- [X] Run the targeted Playwright coverage and confirm it passes.

## Back-End Integration Test

- [X] Add or extend backend integration coverage under `tests/backend/integration/` for transaction realtime fan-out, asserting that a cross-workspace transaction emits realtime transaction/list updates to both workspace groups.
- [X] Add backend integration coverage for the admin-wide `workspacesUpdated` SignalR event, asserting that create/archive/archive-all operations emit the event when the visible workspace list changes.
- [X] Run the targeted backend integration coverage and confirm it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm it passes with no regressions.

## Completion Rule

- [X] This story is complete only when `UI-SHELL-001`, `UI-WS-001`, `UI-TXN-009`, `INT-WS-001`, `INT-WS-002`, `INT-WS-003`, `INT-WS-005`, `INT-WS-006`, and `INT-WS-009` have been checked `- [X]`, the required Playwright coverage for realtime transactions and workspace lists has been added or extended and passes, the required back-end integration coverage for transaction fan-out and `workspacesUpdated` has been added or extended and passes, and the full end-to-end test suite passes with no regressions.
