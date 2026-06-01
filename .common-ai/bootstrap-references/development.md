# Development

## Project Contract

- Product behavior lives in `specs/`.
- Implementation slices live in `stories/`.
- Before behavior changes, update the relevant spec items first. Newly added or changed spec items stay unchecked until implementation and verification complete.
- New or changed spec checklist items get stable IDs such as `CORE-042`.
- New stories start from `stories/_template.story_TODO.md` and reference the spec IDs they implement.

## Prerequisites

- Node.js 20+
- .NET 8 SDK (or the version the project pins)
- Docker + Docker Compose
- PostgreSQL (provided via Docker Compose for local development)

## Workflow Commands

- `npm run validate:workflow` — validates spec frontmatter and IDs, story naming, and story-to-spec references.
- `npm run generate:story-map` — regenerates `stories/00-story-map.md` from active and archived stories.

## Backend (.NET)

- Restore dependencies: `dotnet restore`
- Build: `dotnet build`
- Run dev server: `dotnet run --project backend/<ProjectName>.Api`
- Run tests: `dotnet test`
- Add EF Core migration: `dotnet ef migrations add <Name> --project backend/<ProjectName>.Api`
- Apply migrations: `dotnet ef database update --project backend/<ProjectName>.Api`

## Frontend (React + Vite)

- Install dependencies: `npm --prefix frontend install`
- Run dev server: `npm --prefix frontend run dev`
- Build for production: `npm --prefix frontend run build`
- Lint: `npm --prefix frontend run lint`
- Type-check: `cd frontend && npx tsc -b`

## Docker

- Start full stack: `docker compose up`
- Rebuild and start: `docker compose up --build`
- Stop and remove containers: `docker compose down`

## End-To-End Tests

- Install e2e dependencies: `npm --prefix e2e install`
- Install browsers: `npm --prefix e2e exec playwright install --with-deps chromium`
- Run e2e suite: `npm --prefix e2e exec playwright test`

End-to-end tests must run against the real ASP.NET Core backend and a real PostgreSQL database. Do not replace PostgreSQL with an in-memory database for feature validation. Seed test data through EF Core migrations, real API calls, or SQL fixtures rather than mocking browser-only state.

## Manual Browser QA

Major frontend behavior changes need manual verification in Chrome on the affected route. Exercise the real UI interactions a user would perform — clicking, typing, navigating, form submission, and any collaboration or realtime flows the feature introduces.

## Story Lifecycle

- New story: `NN-slug.story_TODO.md`
- Started story: `NN-slug.story_IN_PROGRESS.md`
- Finished story: `NN-slug.story_COMPLETE.md`

Completed stories move under `stories/archive/` once they are no longer active. After moving, creating, or renaming stories, run:

```sh
npm run generate:story-map
npm run validate:workflow
```

## CI Expectations

CI runs on every PR and must:

1. Build the .NET solution and run `dotnet test`.
2. Build the frontend and run lint + type-check.
3. Spin up PostgreSQL, run EF Core migrations, start the backend, start the frontend, and run Playwright against the real stack.
4. Run `npm run validate:workflow`.
