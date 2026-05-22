## Spec Workflow

- Before implementing a major feature or behavior change, update the relevant file in `specs/`.
- Keep specs split by major feature area.
- Spec checklist items use `- [ ]` for pending work and `- [X]` for implemented work.
- If behavior changes, reflect it in the specs in the same change set.
- Treat `specs/` as a maintained contract, not a one-time planning artifact.

## Story Workflow

- Each implementation slice lives in `stories/` as a `NN-slug.story_<STATUS>.md` file.
- Stories reference the spec IDs they implement (`Implements: CORE-001, CORE-002`).
- A story is not complete while its acceptance checklist still has unchecked items.
- Completed stories move to `stories/archive/`. After moving, creating, or renaming stories, run `npm run generate:story-map` and `npm run validate:workflow`.

## Testing Requirements

- Every major feature requires end-to-end browser coverage.
- End-to-end tests must run against a real ASP.NET Core backend.
- End-to-end tests must run against a real PostgreSQL database.
- Do not replace PostgreSQL with an in-memory database for feature validation.
- Major features must also be manually exercised in a browser after implementation.
- Manual testing should include real UI interactions such as clicking, typing, navigation, form submission, and any collaboration or realtime flows the feature introduces.
- Regressions against existing spec behavior should block completion.

## Agent Working Style

- Prefer small, coherent changes that map clearly to spec items and stories.
- Keep code quality high; do not take shortcuts that weaken maintainability.
- Avoid duplicating code when a shared abstraction belongs in the backend or common layers.
- When adding functionality, think through future extension points.
- If implementation and spec diverge, fix the spec or the code explicitly rather than leaving them inconsistent.

## Current Spec Files

(to be defined)
