# Development

## Project Contract

- Product behavior lives in `specs/`.
- Implementation slices live in `stories/`.
- Before behavior changes, update the relevant spec items first.
- Story files reference the spec IDs they implement.
- After moving, creating, or renaming stories, run `npm run generate:story-map` and `npm run validate:workflow`.

## Prerequisites

- .NET 9 SDK
- Node.js 20+ for CI compatibility; the Admin UI currently declares Node `25.6.0`
- Docker and Docker Compose
- PostgreSQL 16, usually through Docker Compose

## Workflow Commands

- Validate specs and stories: `npm run validate:workflow`
- Regenerate the story map: `npm run generate:story-map`

## Backend

- Restore: `dotnet restore Waterblocks.Api/Waterblocks.Api.csproj`
- Build: `dotnet build Waterblocks.Api/Waterblocks.Api.csproj`
- Run locally: `dotnet run --project Waterblocks.Api/Waterblocks.Api.csproj --urls http://localhost:5671`
- Run integration tests: `dotnet test tests/backend/integration/Waterblocks.IntegrationTests.csproj`
- Add an EF Core migration: `dotnet ef migrations add <Name> --project Waterblocks.Api`
- Apply migrations: `dotnet ef database update --project Waterblocks.Api`

The default local API URL is `http://localhost:5671`. The default connection string expects PostgreSQL on `localhost:5432` with database/user/password `waterblocks`/`postgres`/`postgres`.

## Frontend

- Install dependencies: `npm --prefix waterblocks-admin install`
- Run dev server: `npm --prefix waterblocks-admin run dev`
- Build: `npm --prefix waterblocks-admin run build`
- Lint: `npm --prefix waterblocks-admin run lint`

The Admin UI reads the API URL from `VITE_API_BASE_URL`, then from runtime `window.__WB_CONFIG__.apiBaseUrl`, and otherwise defaults to `http://localhost:5671`.

## Docker

- Full stack: `docker compose -f docker-compose.full.yml up --build`
- Backend dependencies only: `docker compose -f docker-compose.backend.yml up -d`
- API stack for local frontend development: `docker compose -f docker-compose.frontend.yml up --build`
- Wrapper scripts: `./run-compose.ps1 full up` or `./run-compose.sh full up`

## End-To-End Tests

- Start PostgreSQL, for example: `docker compose -f docker-compose.backend.yml up -d`
- Install e2e dependencies: `npm --prefix e2e install`
- Install browser binaries: `npm --prefix e2e exec playwright install chromium`
- Run the smoke suite: `npm --prefix e2e test`

The Playwright config starts the real ASP.NET Core backend and Vite frontend, then runs tests against them. End-to-end tests must use the real backend and PostgreSQL database; do not replace PostgreSQL with an in-memory database for feature validation.

## Story Lifecycle

- New story: `NN-slug.story_TODO.md`
- Started story: `NN-slug.story_IN_PROGRESS.md`
- Finished story: `NN-slug.story_COMPLETE.md`
- Archived stories move under `stories/archive/`.

## CI Expectations

CI builds the backend, runs backend integration tests, builds the Admin UI, validates the spec/story workflow, builds Docker images, and runs the Playwright smoke suite against the real API/frontend/PostgreSQL stack.
