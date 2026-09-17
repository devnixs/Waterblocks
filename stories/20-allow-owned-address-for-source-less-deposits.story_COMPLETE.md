---
id: STORY-020
title: Allow owned destination addresses for source-less deposits
target_specs:
  - specs/03-core-workflows.spec.md
  - specs/06-testing-and-qa.spec.md
status: COMPLETE
---

## Goal

- [X] Let testers send a source-less Coinbase BTC deposit to a specific address belonging to a Waterblocks vault, including a secondary BTC address on a vault with multiple addresses.

## Scope

- [X] Keep the Admin UI one-time destination option enabled when the Coinbase/exchange source is selected.
- [X] Preserve the entered destination address instead of replacing it with a vault’s default deposit address.
- [X] Validate on the backend that the entered address belongs to a Waterblocks vault wallet for the selected BTC asset.
- [X] Continue rejecting source-less deposits to external or unknown destination addresses with `SOURCELESS_DESTINATION_REQUIRED`.
- [X] Credit the wallet owning the exact entered address and return that address from Fireblocks-compatible transaction polling.
- [X] Check `ADM-TXN-015`, `UI-TXN-010`, and `TEST-014` from `- [ ]` to `- [X]` only after implementation and verification are complete.

## Relevant Specs

- [X] `specs/03-core-workflows.spec.md` (`ADM-TXN-015`, `UI-TXN-010`)
- [X] `specs/06-testing-and-qa.spec.md` (`TEST-014`)

## Acceptance Notes

- [X] A selected destination vault may continue using its default deposit address.
- [X] A manually entered one-time destination must be accepted only when ownership resolution finds that exact address under the selected asset.
- [X] BTC vaults with multiple addresses must allow any owned address to be targeted independently.
- [X] The source-less Fireblocks response contract introduced by STORY-019 must remain unchanged.

## Playwright Test

- [X] Extend `e2e/tests/source-less-btc-deposit.spec.ts` to create a secondary BTC address, select Coinbase/exchange as the source and One-time address as the destination, enter the secondary address, submit, and assert the created transaction displays that exact destination.
- [X] Verify the one-time destination option remains enabled for the Coinbase/exchange source.
- [X] Run the targeted Playwright test and confirm it passes.

## Back-End Integration Test

- [X] Extend `tests/backend/integration/SourceLessBtcDepositTests.cs` to create multiple BTC addresses for one vault, create a source-less deposit targeting a non-default owned address, verify the owning wallet is credited, and assert Fireblocks polling returns the entered destination address and vault metadata.
- [X] Add or retain a negative case proving an unowned destination address is rejected with `SOURCELESS_DESTINATION_REQUIRED`.
- [X] Run the targeted back-end integration tests and confirm they pass.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm it passes with no regressions.

## Workflow Validation

- [X] Run `npm run generate:story-map`.
- [X] Run `npm run validate:workflow`.

## Completion Rule

- [X] This story is complete only when `ADM-TXN-015`, `UI-TXN-010`, and `TEST-014` have been checked `- [X]`, the required Playwright coverage for targeting an owned secondary BTC address has been added and passes, the required backend integration coverage has been added and passes, the full end-to-end suite passes with no regressions, and workflow validation succeeds.
