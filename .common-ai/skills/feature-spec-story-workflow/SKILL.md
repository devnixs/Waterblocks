---
name: feature-spec-story-workflow
description: Prepare feature work by updating or creating the relevant product specs first and then drafting one or more implementation stories that point back to those specs. Use when asked to create a feature, modify existing feature behavior, plan a feature change, or bring implementation planning in line with spec-driven workflows that use `specs/` and `stories/` directories.
---

# Feature Spec Story Workflow

Follow this workflow whenever the task is feature creation or feature modification in a repo that treats specs as the product contract.

## Inspect The Existing Contract

- Read the repo instructions first if files such as `AGENTS.md`, `CLAUDE.md`, or similar guidance exist.
- Read `specs/index.md` first when it exists, then use spec frontmatter (`area`, `owners`, `status`, `depends_on`) to inspect only the relevant spec files before reading broadly.
- Inspect the existing `stories/` directory and `stories/_template.story_TODO.md` before making assumptions about naming, formatting, or completion rules.
- Identify which existing spec files describe the affected behavior.
- Identify whether the change fits an existing story or requires a new story. Prefer a new story when the work is a distinct feature slice or when the change is large enough to deserve isolated tracking.

## Update Specs First

- Update the relevant spec files before proposing implementation work.
- Add new spec files if the feature introduces a new major behavior area that does not fit the current split.
- Update `specs/index.md` when adding a new spec file or major spec area.
- Maintain YAML frontmatter at the top of every spec file:
  - `area`: stable feature-area slug
  - `owners`: one or more responsible domains such as `frontend`, `backend`, `qa`, `product`, or `devops`
  - `status`: `active`, `draft`, `deprecated`, or `archived`
  - `depends_on`: list of related spec paths, or `[]`
- Write specs as checklist items using `- [ ]` and `- [X]`.
- Give every new or changed spec checklist item a stable requirement ID in the form `PREFIX-001`, using the prefix assigned in `specs/index.md`.
- Keep existing requirement IDs stable when rewording behavior; do not renumber IDs for cosmetic ordering.
- If an older checklist item does not yet have a stable ID and a story touches it, add an ID to that item as part of the spec update.
- If you modify an existing spec item because behavior is changing, convert that item back to `- [ ]`.
- If you add new spec items, add them as `- [ ]`.
- If you create a new spec file, all checklist items in that file must start as `- [ ]`.
- Do not mark changed or new spec items complete during planning. They remain pending until the feature is implemented and verified.
- Keep spec wording behavior-focused. Describe observable product behavior, not implementation details unless the repository's specs already mix both intentionally.

## Create Implementation Stories

- After the specs are updated, create the implementation story or stories.
- Split into multiple stories when the change is too large for one coherent delivery slice, spans multiple subsystems, or would benefit from independent testing and sequencing.
- Store stories in the repository's established `stories/` location unless the repo uses a different convention.
- Start from `stories/_template.story_TODO.md` when it exists, then adapt it to the story.
- Match the local story format if one already exists. Reuse local headings and tone instead of inventing a new template.
- Each story must reference the exact spec file or files and stable requirement IDs that were updated for that slice.
- Each story must explicitly say that completion requires checking the referenced spec items from `- [ ]` to `- [X]` when the work is actually done.
- Each behavior-changing story must mention all three verification activities that need to be performed for that feature: Playwright coverage, the manual Chrome DevTools browser test, and the full e2e regression run.
- All implementation items inside a story file must be written as `- [ ]` checkboxes. They start unchecked and are checked off (`- [X]`) as each item is completed during implementation.
- Story filenames must end with a status suffix: `_TODO` when first created, `_IN_PROGRESS` once work has begun, and `_COMPLETE` when all items are checked and verification has passed. Rename the file as the status changes.
- Keep story filenames in the form `NN-slug.story_STATUS.md`; do not reuse story numbers.
- When the repository has a story-map generator, run it after creating, renaming, archiving, or completing stories. In this repository, run `npm run generate:story-map`.

## Scale And Archiving

- Keep active work easy to find. If the repository accumulates many completed stories, move older completed stories into the repository's established archive location or create one before the folder becomes noisy.
- Do not archive stories that are still `_TODO` or `_IN_PROGRESS`.
- Prefer exact spec ID references over copying large chunks of spec text into stories. This keeps stories compact and keeps specs as the source of truth.
- Use spec frontmatter dependencies to decide which neighboring specs need review for cross-cutting changes.
- For large changes, create multiple stories that each reference the exact spec IDs they own. Avoid one broad story that points at an entire spec file unless the entire file is genuinely in scope.

## Require Three Verification Activities

Every behavior-changing story must require all three of the following before it can be considered complete. None of them is optional polish for user-facing behavior.

### 1. Playwright Coverage

- Each behavior-changing story must add or extend Playwright coverage for the new feature or bugfix.
- Place the test in the repository's established Playwright location (for example `e2e/tests/`) and follow local naming and structure conventions.
- The test must exercise the actual user-visible behavior described in the spec items the story references, not just internal implementation details.
- The story must describe what the Playwright coverage should cover in concrete terms (route, setup, actions, assertions).
- The Playwright coverage must pass before the story is marked complete.

### 2. Manual Chrome DevTools Browser Test

- The required manual browser test must be described inside each story in concrete terms. Name the route, user actions, and expected outcome.
- The manual browser test must be performed using the Chrome DevTools MCP browser instance, not just by reading code or relying on automated tests.
- A story is not complete until that manual browser verification has been executed and the feature behaves as expected.

### 3. Full E2E Regression Run

- Each story must require running the full end-to-end test suite (not just the new test) to confirm that the change has not introduced a regression in any existing feature.
- The story must name the exact command used to run the full e2e suite in this repository.
- All e2e tests must pass before the story is marked complete. If any existing test fails, the story is not done until the regression is fixed or the test is intentionally and justifiably updated.
- If the repository already has an end-to-end or QA spec, update or reference it when the new feature changes required browser coverage.

## Story Content Checklist

Each story should cover:

- Goal
- Scope
- Relevant specs
- Exact stable requirement IDs for the referenced spec items
- Acceptance notes or implementation constraints when needed
- Playwright test plan: which file is added or extended, what it covers, what assertions it makes
- Browser test instructions with concrete manual steps using Chrome DevTools MCP
- Full e2e regression instructions: exact command to run the full suite
- Completion rule stating all four conditions:
  1. referenced spec items are checked to `- [X]`
  2. the Playwright coverage has been added or extended and passes
  3. the Chrome DevTools manual browser test has been run and passed
  4. the full e2e test suite has been run and all tests pass

## Suggested Wording Patterns

Use wording like:

- `Relevant Specs`: list exact `specs/...` paths and stable IDs such as `TOOLS-001`
- `Playwright Test`: describe the new test file/case, the user flow it drives, and the assertions it makes
- `Browser Test`: describe the route, setup, clicks, typing, gestures, collaboration steps, and expected visible result
- `E2E Regression`: name the command (e.g. `npx playwright test`) and require the entire suite to pass
- `Completion Rule`: `This story is complete only when the referenced spec IDs have been checked - [X], the new Playwright test described below has been added or extended and passes, the manual Chrome DevTools browser test described below has been run successfully, and the full e2e test suite passes with no regressions.`

## Workflow Validation

- Run the repository's workflow validator when one exists before marking a story complete. In this repository, run `npm run validate:workflow`.
- If the repository has a generated story map, regenerate it before validation. In this repository, run `npm run generate:story-map` first.
- Treat validation failures as workflow defects that must be fixed before closure.
- Validation warnings for legacy items may be acceptable only when the current story did not touch those items.

## Output Expectations

When using this skill for a real task:

- Produce the spec edits first.
- Produce the story file or files second.
- Keep changed specs unchecked until implementation and verification are done.
- Keep the story explicit about which spec IDs should be checked after completion.
- If the feature is large, explain the story split briefly so sequencing is clear.
- All items inside the story file are `- [ ]` checkboxes; check them off as work is done.
- Name new story files with the `_TODO` suffix. Rename to `_IN_PROGRESS` when implementation starts and to `_COMPLETE` when all checks pass.
- Run `npm run generate:story-map` when available after story file changes.
- Run `npm run validate:workflow` when available and report the result.
- Do not implement that newly created story yet; just create the story file with the checklist and verification instructions. Implementation comes later, either by you or by an engineer who picks up the story.