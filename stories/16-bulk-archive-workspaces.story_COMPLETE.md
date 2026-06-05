---
id: STORY-016
title: Bulk archive workspaces from the Workspaces page
target_specs:
  - specs/01-routing-and-navigation.spec.md
  - specs/03-core-workflows.spec.md
  - specs/07-deployment-and-ops.spec.md
status: COMPLETE
---

## Goal

Add an env-gated bulk action on the admin Workspaces page that archives all workspaces with one click while preserving the `Default` workspace.

## Scope

- [X] Add a bulk workspace-archive admin API action that archives every non-deleted workspace except `Default`, reusing the existing soft-delete semantics from single-workspace archive.
- [X] Gate the bulk archive capability behind the `ARCHIVE_ALL_WORKSPACES_ENABLED` environment variable so the backend rejects the action when the flag is absent or false and the frontend keeps the control hidden in that state.
- [X] Update the Workspaces page to show a top-of-page button named `Archive all workspaces` only when the runtime config flag is enabled.
- [X] Require confirmation before the bulk action runs, then refresh the workspace list and show toast feedback for success or failure.
- [X] Confirm that the `Default` workspace remains visible and unarchived after the bulk action completes.
- [X] Check `UI-WS-002`, `ADM-WS-004`, and `OPS-007` in their respective spec files from `- [ ]` to `- [X]` only after implementation and verification are complete.

## Relevant Specs

- [X] `specs/01-routing-and-navigation.spec.md` (`UI-WS-002`)
- [X] `specs/03-core-workflows.spec.md` (`ADM-WS-004`)
- [X] `specs/07-deployment-and-ops.spec.md` (`OPS-007`)

## Acceptance Notes

- [X] The button label must be exactly `Archive all workspaces`.
- [X] The bulk action belongs at the top of the `/workspaces` page, above the list of workspace cards.
- [X] The feature is considered disabled unless `ARCHIVE_ALL_WORKSPACES_ENABLED` resolves to true in both the backend configuration and the frontend runtime config.
- [X] The exclusion rule is name-based: the workspace named `Default` must not be archived by the bulk action.
- [X] Single-workspace archive behavior must continue to work unchanged.

## Playwright Test

- [X] Add or extend Playwright coverage in `e2e/tests/` to cover the `/workspaces` bulk-archive flow with the feature flag enabled: seed `Default` plus at least two additional workspaces, open `/workspaces`, click `Archive all workspaces`, confirm the action, and assert the non-default workspaces disappear while `Default` remains visible.
- [X] Add a Playwright assertion that the `Archive all workspaces` button is not rendered when the frontend runtime config does not enable the feature.
- [X] Run the targeted Playwright coverage and confirm it passes.

## Back-End Integration Test

- [X] Add or extend backend integration coverage under `tests/backend/integration/` for `POST /admin/workspaces/archive-all`, asserting that: enabled flag archives all eligible workspaces; the `Default` workspace remains unarchived; already deleted workspaces are ignored; and the response uses the admin success envelope.
- [X] Add a backend integration assertion that the same endpoint returns the documented admin error when `ARCHIVE_ALL_WORKSPACES_ENABLED` is absent or false and that no additional workspaces are archived in that case.
- [X] Run the targeted backend integration coverage and confirm it passes.

## E2E Regression

- [X] Run the full end-to-end suite with `cd e2e && npm test` and confirm it passes with no regressions.

## Completion Rule

- [X] This story is complete only when `UI-WS-002`, `ADM-WS-004`, and `OPS-007` have been checked `- [X]`, the required Playwright coverage for the Workspaces page bulk-archive flow has been added or extended and passes, the required back-end integration coverage for `POST /admin/workspaces/archive-all` has been added or extended and passes, and the full end-to-end test suite passes with no regressions.
