---
id: STORY-011
title: Frontend unit tests for admin API client and React Query hooks
target_specs:
  - specs/06-testing-and-qa.spec.md
  - specs/00-foundations.spec.md
status: TODO
---

## Goal

The admin API client (`waterblocks-admin/src/api/adminClient.ts`) and React Query hooks (`waterblocks-admin/src/api/queries.ts`) underpin every UI page (`UI-FND-002..005`) but have no unit coverage. Add Vitest unit tests for the fetch wrapper (error envelope handling, X-Workspace-Id header, workspace-gated hook enabling, `useTransitionTransaction` action dispatch) and record the suite in `specs/06-testing-and-qa.spec.md`.

## Scope

- Vitest setup under `tests/frontend/unit/`.
- Mocked `fetch` to assert header injection and error normalization.
- React Query hook tests using `@tanstack/react-query`'s test utilities.

## Relevant Specs

- `specs/00-foundations.spec.md` (UI-FND-002..005)
- `specs/06-testing-and-qa.spec.md` (new TEST-* entry)

## Acceptance Notes

- Tests run via `npm test` in `waterblocks-admin/` and from a root `npm run test` if wired.

## Acceptance Criteria

- [ ] Vitest config + harness in `tests/frontend/unit/`.
- [ ] At least one test per UI-FND item.
- [ ] New `TEST-*` checklist entry in `specs/06-testing-and-qa.spec.md`.

## Playwright Test

N/A (unit story).

## Browser Test

N/A.

## E2E Regression

Covered by Story 10.

## Completion Rule

Story is complete when Vitest suite passes locally and in CI and the new `TEST-*` checklist item is checked.
