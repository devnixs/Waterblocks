---
name: greenfield-spec-bootstrap
description: Bootstrap a brand new React + .NET project from an empty (or nearly empty) repository into a spec-driven structure. Captures the product vision through interview or supplied description, then generates initial specs (desired behavior, stable IDs, frontmatter), implementation stories ordered for delivery, validation scripts, Playwright end-to-end scaffolding, CI hooks, and development docs. Use this skill whenever the user wants to start a new project from scratch, set up a fresh repo, kick off a greenfield project, scaffold a spec-driven workflow on an empty repository, or "begin a new app" — even when they don't say the word "spec".
---

# Greenfield Spec Bootstrap

Use this skill when starting a project from an empty or nearly empty repository. The output is a spec-driven workflow scaffold: specs describing the desired product, stories that implement it incrementally, and validation that keeps both honest as the project grows.

Assumed stack: React + Vite + TypeScript frontend, ASP.NET Core backend, PostgreSQL, Playwright for end-to-end tests. The defaults below assume this stack; if the user is on a meaningfully different stack, adapt commands and scaffolding accordingly and confirm before proceeding.

## Core Rules

- Specs describe **desired** behavior. Checklist items start unchecked because nothing has been built yet.
- Stories slice the specs into incremental, deliverable units. Each story references the spec IDs it implements.
- Set up the workflow scaffolding before writing implementation code. The first stories themselves cover the scaffolding work (.NET solution init, React app init, Docker, CI), so the spec/story workflow is in place before any feature work begins.
- Use stable requirement IDs and spec frontmatter from the first spec.
- Add validation early so CI can enforce it from the first PR.
- Capture the product vision in writing so future contributors understand why the specs exist.

## Phase 1: Capture The Product Vision

Before scaffolding anything, the skill needs a concrete product description. Sources, in order:

1. **The conversation so far.** If the user already described what they want to build, summarize it back in 3-5 bullet points and ask them to confirm or correct. Do not skip the confirmation step — bootstrapped specs become load-bearing very quickly.
2. **A short interview.** If the description is thin, ask focused questions about:
   - product purpose, target users, primary value proposition
   - the 3-5 core user workflows that define the first usable version
   - key domain entities and what data must persist
   - auth model (none, basic password, SSO, API key + JWT, etc.)
   - third-party integrations (payments, blockchain, email, etc.)
   - deployment target (Docker Compose, cloud, on-prem)
   - explicit non-goals — what is out of scope for the first release

Lead with 3-4 questions, fill the gaps, then confirm. Do not ask everything at once.

Save the agreed product description to `docs/product-vision.md` so subsequent phases reference one source of truth.

## Phase 2: Create Baseline Structure

Create these directories:

```text
backend/                       # .NET solution will live here; do not initialize yet
frontend/                      # React app will live here; do not initialize yet
e2e/                           # Playwright config and tests
specs/
stories/
stories/archive/
scripts/
docs/
.github/workflows/
```

Copy or adapt from shared reference templates under `.common-ai/bootstrap-references/`:

- `scripts/validate-workflow.js` — from `.common-ai/bootstrap-references/scripts/validate-workflow.js`
- `scripts/generate-story-map.js` — from `.common-ai/bootstrap-references/scripts/generate-story-map.js`
- `docs/development.md` — from `.common-ai/bootstrap-references/development.md`, edited if the user wants different commands
- root `package.json` — from `.common-ai/bootstrap-references/package.json`, used purely as a workflow orchestrator for validation and story-map generation
- root `AGENTS.md` — read `.common-ai/bootstrap-references/AGENTS.md` and merge its spec/story/testing/agent guidance into the current root `AGENTS.md`; if no root file exists, create it from the reference
- `stories/_template.story_TODO.md` — story template referenced by future stories

Do **not** initialize the .NET solution or React app in this phase. Those are tracked by the first implementation stories so the project history shows them being built deliberately rather than appearing fully-formed.

When merging `AGENTS.md`, preserve any existing project-specific guidance and add the missing reference sections rather than replacing the file. Update the reference's `Current Spec Files` section after specs are generated so it lists the actual spec files for this project.

## Phase 3: Define Spec Areas

Pick spec areas based on the product vision. A reasonable default for React + .NET projects:

- `specs/00-foundations.spec.md` — naming, error envelope, configuration approach, base conventions
- `specs/01-routing-and-navigation.spec.md` — frontend routes and navigation
- `specs/02-auth-and-identity.spec.md` — only if auth is in the first release
- `specs/03-core-workflows.spec.md` — primary user-facing workflows
- `specs/04-data-and-persistence.spec.md` — entities, schema, migrations
- `specs/05-integrations.spec.md` — only if third-party integrations are in scope
- `specs/06-testing-and-qa.spec.md` — testing strategy and coverage expectations
- `specs/07-deployment-and-ops.spec.md` — Docker, CI, env config
- `specs/08-visual-design.spec.md` — design tokens, primary screens

Drop areas that do not apply. Add product-specific areas if the vision needs them (for example, a payments-heavy product might want a dedicated `specs/09-payments.spec.md`). The point is one area per coherent slice of product behavior, not one area per template entry.

Each spec file starts with frontmatter:

```yaml
---
area: core-workflows
owners:
  - frontend
  - backend
status: draft
depends_on:
  - specs/00-foundations.spec.md
---
```

Use `status: draft` while the user is still reviewing. Flip to `active` once the baseline is accepted.

## Phase 4: Assign Stable Requirement Prefixes

Record the prefix-to-area mapping in `specs/index.md`. Suggested defaults:

- `FOUND` — foundations
- `ROUTE` — routing
- `AUTH` — auth
- `CORE` — core workflows
- `DATA` — data and persistence
- `INT` — integrations
- `QA` — testing
- `DEPLOY` — deployment
- `DESIGN` — visual design

IDs are zero-padded three digits (`CORE-001`, `CORE-002`, …) and are never reused even if an item is later removed.

## Phase 5: Write The Specs

For each spec area, write 5-15 checklist items describing desired behavior. All items start unchecked because nothing has been built yet. Wording should be observable behavior, not implementation tasks.

```md
- [ ] CORE-001: Users can register a new account from the landing page using email and password.
- [ ] CORE-002: Authenticated users see a personalized dashboard listing their projects.
- [ ] DATA-001: Project records persist in PostgreSQL and survive backend restarts.
- [ ] DATA-002: Schema migrations are version-controlled and applied automatically on backend startup in non-production environments.
```

Good vs. bad spec items:

- Good: `AUTH-003: Users can reset their password through an email link valid for 24 hours.` — observable, testable, behavior-shaped.
- Bad: `AUTH-003: Use bcrypt with 12 rounds for password hashing.` — that is an implementation choice and belongs in a story or ADR, not a behavior spec.

If the user has not yet decided some behavior, leave the item unchecked with a short note such as `Open question: confirm reset link expiry window`. Open questions in specs are healthier than guesses that quietly become "decisions".

## Phase 6: Generate Implementation Stories

This is where greenfield bootstrap differs from a legacy mapping — the skill produces an initial set of stories so the user can start executing immediately.

Each story should:

- represent roughly one to three days of work
- reference the spec IDs it delivers (in the story body)
- be ordered so earlier stories establish foundations later stories rely on

A sensible opening sequence for a React + .NET greenfield, regardless of product:

1. `01-scaffold-dotnet-solution.story_TODO.md` — `dotnet new sln`, Web API project under `backend/`, base controller layout, `appsettings.json`, dotenv wiring if used.
2. `02-scaffold-react-frontend.story_TODO.md` — `npm create vite@latest` for React + TS under `frontend/`, base routing, lint, tsconfig, type-check.
3. `03-docker-compose-and-postgres.story_TODO.md` — `docker-compose.yml` with backend, frontend, PostgreSQL, env files.
4. `04-ef-core-baseline-and-first-migration.story_TODO.md` — EF Core wiring, connection string, initial migration, `dotnet ef` scripts.
5. `05-ci-pipeline.story_TODO.md` — GitHub Actions workflow: dotnet build, dotnet test, frontend build, lint, type-check, validate-workflow, Playwright smoke.
6. `06-e2e-smoke-test.story_TODO.md` — Playwright config + one smoke test hitting the real backend.

After the opening sequence, generate feature stories from the spec items. A feature story typically covers one to a few related spec IDs from the same area. Order them so the dependency graph in the spec frontmatter is respected — a story implementing `CORE-*` cannot land before the foundations and data stories it relies on.

Each story file starts from `stories/_template.story_TODO.md` and includes:

- a short description of the deliverable
- the spec IDs implemented (`Implements: CORE-001, CORE-002`)
- an acceptance checklist (specific enough to be verified, derived from the spec items)
- testing notes (which spec items get e2e coverage in this story)

After creating stories, run `node scripts/generate-story-map.js` (or `npm run generate:story-map`) to produce `stories/00-story-map.md`.

## Phase 7: Add Validation

`scripts/validate-workflow.js` checks:

- spec frontmatter exists and has valid `area`, `owners`, `status`, and `depends_on`
- spec checklist items have stable IDs and IDs are unique within and across spec files
- stories follow `NN-slug.story_STATUS.md`
- stories reference spec IDs that actually exist
- completed stories (`_COMPLETE`) have no unchecked checklist items

Expose it through the root `package.json`:

```json
{
  "scripts": {
    "validate:workflow": "node scripts/validate-workflow.js",
    "generate:story-map": "node scripts/generate-story-map.js"
  }
}
```

## Phase 8: Add End-To-End Testing Scaffolding

Scaffold Playwright under `e2e/`:

- `e2e/package.json` with `@playwright/test`
- `playwright.config.ts` reading `BASE_URL` for the frontend and a separate environment variable for the backend health check
- one smoke test that loads the root route and asserts the app shell renders

End-to-end tests must run against the real stack:

- real ASP.NET Core backend process
- real PostgreSQL database (the same major version as production)
- no in-memory database substitution for feature validation
- seed data via EF Core migrations, real API calls, or SQL fixtures — not browser-only state

The full Playwright suite gets expanded by feature stories; the bootstrap only produces the scaffold and one smoke test.

CI workflow (`.github/workflows/ci.yml`) sequence:

1. checkout
2. set up Node and the .NET SDK
3. start PostgreSQL as a service container
4. run EF Core migrations
5. build and run the backend
6. build and run the frontend
7. wait for readiness
8. install Playwright browsers
9. run Playwright
10. run `npm run validate:workflow`

If the user wants CI deferred to a later story, leave the workflow file with a clear `# TODO:` header rather than producing a half-configured pipeline.

## Phase 9: Document The Workflow

`docs/development.md` should cover:

- prerequisites (Node, .NET SDK, Docker, PostgreSQL)
- backend setup, build, test, EF Core commands
- frontend setup, dev server, build, lint, type-check
- e2e install and run, including the real-backend + real-PostgreSQL requirement
- validation commands
- story lifecycle (`_TODO` → `_IN_PROGRESS` → `_COMPLETE` → archived)
- how to add new spec items and stories that reference them

Start from `.common-ai/bootstrap-references/development.md` and edit to match the actual project commands.

## Output Expectations

For a real greenfield bootstrap, produce:

1. `docs/product-vision.md` capturing the agreed product description.
2. `specs/` with `index.md`, prefix mapping, frontmatter on every file, and all items unchecked because nothing is implemented yet.
3. `stories/` with `_template.story_TODO.md`, the opening scaffolding stories from Phase 6, and feature stories generated from the spec items.
4. `stories/00-story-map.md` regenerated from the current stories.
5. `scripts/validate-workflow.js` and `scripts/generate-story-map.js`.
6. Root `package.json` and `AGENTS.md`.
7. `e2e/` with Playwright config, `package.json`, and one smoke test, configured for the real backend and real PostgreSQL.
8. `.github/workflows/ci.yml` running build, validation, and e2e against the real stack.
9. `docs/development.md` tailored for React + .NET.
10. A short summary of which spec items the initial story sequence covers and which remain to be planned in future stories.

After bootstrap, the next step is to start executing the scaffolding stories — that is where backend and frontend get initialized for the first time. Do not initialize them during bootstrap itself.
