---
id: STORY-013
title: Scope GET /v1/vault/.../unspent_inputs to workspace
target_specs:
  - specs/03-core-workflows.spec.md
status: TODO
---

## Goal

`VAULT-014` (item ITEM-0029, `Waterblocks.Api/Controllers/FireblocksCompatible/UnspentInputsController.cs`) was recorded with the note "no workspace scoping in controller". Every other Fireblocks-compatible vault endpoint scopes by workspace via `X-API-Key`; this endpoint should match. Add workspace scoping and a regression test under `tests/backend/integration/`.

## Scope

- Add workspace resolution to `UnspentInputsController`.
- Return `VAULT_NOT_FOUND` (or equivalent) when the vault is not in the caller's workspace, mirroring `VaultWalletsController`.
- Add an integration test covering: same-workspace -> empty list; cross-workspace -> not found; missing API key -> 401.

## Relevant Specs

- `specs/03-core-workflows.spec.md` (VAULT-014)
- `specs/06-testing-and-qa.spec.md` (new TEST-* entry, optional)

## Acceptance Notes

- Update the `VAULT-014` checklist text to remove the "no workspace scoping" caveat once the controller behaves like its siblings.

## Acceptance Criteria

- [ ] Controller reads workspace context and filters wallets accordingly.
- [ ] Integration test verifies isolation + 401 on missing key.
- [ ] `VAULT-014` text in `specs/03-core-workflows.spec.md` updated.

## Playwright Test

N/A.

## Browser Test

N/A.

## E2E Regression

Covered indirectly by Story 12 / existing workspace-isolation suite.

## Completion Rule

Controller scoped, test added, spec updated, all relevant checklist items checked.
