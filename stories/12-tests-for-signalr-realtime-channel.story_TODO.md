---
id: STORY-012
title: Integration tests for SignalR realtime channel
target_specs:
  - specs/06-testing-and-qa.spec.md
  - specs/05-integrations.spec.md
status: TODO
---

## Goal

The SignalR `/hubs/admin` hub and its events (`INT-WS-001..006`) ship without any automated integration coverage. Add tests that subscribe to the hub from `WebApplicationFactory<Program>` and assert that workspace group join/leave plus the `transactionUpserted`, `transactionsUpdated`, `vaultUpserted`, and `vaultsUpdated` events fire when corresponding admin operations execute.

## Scope

- Hub connection helper added to `tests/backend/integration/Infrastructure/`.
- Tests covering each of the 5 hub events on real domain actions.
- Workspace scoping: a hub client joined to workspace A must NOT receive events for workspace B.

## Relevant Specs

- `specs/05-integrations.spec.md` (INT-WS-001..005)
- `specs/06-testing-and-qa.spec.md` (new TEST-* entry)

## Acceptance Notes

- Tests use `Microsoft.AspNetCore.SignalR.Client` against the in-memory `TestServer`.

## Acceptance Criteria

- [ ] Helper for opening a `HubConnection` against the test factory.
- [ ] One test per event verifying payload shape + group scoping.
- [ ] New `TEST-*` checklist entry in `specs/06-testing-and-qa.spec.md`.

## Playwright Test

N/A.

## Browser Test

N/A.

## E2E Regression

Covered by Story 10 in addition to backend integration.

## Completion Rule

All five hub events have at least one passing integration test and the new `TEST-*` checklist item is checked.
