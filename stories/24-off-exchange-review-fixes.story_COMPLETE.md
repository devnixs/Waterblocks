---
id: STORY-024
title: Apply the PR review fixes of the Off Exchange work
target_specs:
  - specs/03-core-workflows.spec.md
  - specs/04-data-and-persistence.spec.md
  - specs/06-testing-and-qa.spec.md
  - specs/07-deployment-and-ops.spec.md
status: COMPLETE
---

## Goal

- [X] Ship the Off Exchange schema as one migration, so that a deploy or a rollback has one step.
- [X] Let a Helm install connect exchange accounts and mirror collateral, as a Docker Compose install does.
- [X] Make the destination address of a removal deterministic, and make the id lookups of the collateral calls refuse duplicate rows.

## Scope

- [X] Merge `AddOffExchangeCollateral` and `AddExchangeCatalog` into one migration, `AddOffExchangeCollateral`, with one `Up`, one `Down`, one designer file and the snapshot. Keep both schema changes.
- [X] Add `api.exchangeSimulators.<EXCHANGE_TYPE>` values to the Helm chart, with `GATEIO` as the default entry. The API deployment gets `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__BASE_URL` and `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__ADMIN_TOKEN`. The token comes from `existingSecret` or from a chart Secret. The chart version is 0.1.2.
- [X] A removal pays the first address of the vault wallet in the order of `VaultAddressesController`: the BIP44 index first, with addresses that have no index last, then the id.
- [X] The exchange account and vault lookups of the collateral calls use `SingleOrDefaultAsync`.

## Relevant Specs

- [X] `specs/03-core-workflows.spec.md` (`EXCH-008`)
- [X] `specs/04-data-and-persistence.spec.md` (`DATA-MIG-001`)
- [X] `specs/06-testing-and-qa.spec.md` (`TEST-030`)
- [X] `specs/07-deployment-and-ops.spec.md` (`OPS-013`)

## Acceptance Notes

- [X] A database that applied an earlier migration of this branch must be recreated. No shared database applied them.
- [X] `helm lint` and `helm template` with Helm 3.16.4 pass for the default values, an inline token, an existing Secret and a second exchange. A key that is not an exchange type fails the render.

## Playwright Test

- [X] The screens do not change, so no Playwright test is added.

## Back-End Integration Test

- [X] Add `Remove_Pays_The_First_Address_Of_The_Vault_Address_List` to `OffExchangeCollateralTests`. Without the address order it fails.
- [X] Run the full integration test project and confirm that it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm that it passes with no regressions.

## Workflow Validation

- [X] Run `npm run generate:story-map`.
- [X] Run `npm run validate:workflow`.

## Completion Rule

- [X] This story is complete only when the referenced spec IDs have been checked `- [X]`, the back-end integration coverage has been added and passes, the full end-to-end suite passes with no regressions, and workflow validation succeeds.
