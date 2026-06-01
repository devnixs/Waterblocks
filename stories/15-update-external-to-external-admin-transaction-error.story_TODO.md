---
id: STORY-015
title: Update external-to-external admin transaction error message
target_specs:
  - specs/03-core-workflows.spec.md
status: TODO
---

## Goal

Change the `INVALID_TRANSACTION_SCOPE` message returned by `POST /admin/transactions` when both source and destination resolve as external addresses. The error should explain that the caller is trying to create an external-to-external transaction and ask whether the destination address exists.

## Scope

- [X] Update `Waterblocks.Api/Services/AdminTransactionService.cs` so the `INVALID_TRANSACTION_SCOPE` error message is exactly: "You are trying to create a transaction from an external address to another external address. Are you sure the destination address exists?"
- [X] Add or update focused backend coverage for the external-to-external admin transaction rejection, asserting both the `INVALID_TRANSACTION_SCOPE` code and the exact message.
- [X] Check `ADM-TXN-004` in `specs/03-core-workflows.spec.md` from `- [ ]` to `- [X]` only after the implementation and verification are complete.

## Relevant Specs

- `specs/03-core-workflows.spec.md` (`ADM-TXN-004`)

## Acceptance Notes

- [X] Preserve the existing validation behavior: admin-created transactions still require at least one side to be internal.
- [X] Only the user-facing error message changes; the error code remains `INVALID_TRANSACTION_SCOPE`.

## Playwright Test

- [X] Add or extend Playwright coverage for the admin transaction creation flow that submits a transaction with external source and external destination addresses, then assert the visible error text matches the required message.
- [X] Run the targeted Playwright test and confirm it passes.

## Browser Test

- [ ] Using Chrome DevTools MCP, open the admin transactions route, attempt to create a transaction with both source and destination as external/free-text addresses, submit the form, and confirm the displayed error is: "You are trying to create a transaction from an external address to another external address. Are you sure the destination address exists?"

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm it passes with no regressions.

## Completion Rule

- [ ] This story is complete only when `ADM-TXN-004` has been checked `- [X]`, the Playwright coverage described above has been added or extended and passes, the manual Chrome DevTools browser test described above has been run successfully, and the full e2e test suite passes with no regressions.
