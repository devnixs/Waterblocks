---
area: deployment-and-ops
owners:
  - devops
status: active
depends_on:
  - specs/00-foundations.spec.md
---

## Docker & Compose

- [X] OPS-001: `docker-compose.yml` is the default single-host stack: postgres 16-alpine (env POSTGRES_USER/PASSWORD/DB defaulted to `postgres`/`postgres`/`waterblocks`, pg_isready healthcheck, named volume `postgres_data`, port 5432) and `api` built from `Waterblocks.Api/Dockerfile` exposing 5671->8080 with `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__DefaultConnection` pointing at the postgres service, optional `DATADOG_API_KEY` passthrough, `restart: unless-stopped`, gated on postgres health. Source: `docker-compose.yml:1-39`. Confidence: high.
- [X] OPS-002: `docker-compose.backend.yml` is the database-only variant: postgres 16-alpine with the same env/healthcheck/volume/port as the default stack and no api/admin services (intended for `dotnet run` against a containerised DB). Source: `docker-compose.backend.yml:1-23`. Confidence: high.
- [X] OPS-003: `docker-compose.frontend.yml` mirrors the default stack (postgres + api on 5671->8080) but no admin UI container, intended for running the Vite dev server locally against a containerised backend. Source: `docker-compose.frontend.yml:1-39`. Confidence: high.
- [X] OPS-004: `docker-compose.full.yml` is the all-in-one stack: postgres + api (5671->8080) + `admin` built from `waterblocks-admin/Dockerfile` exposing 5173->80, restart `unless-stopped`, depends on api. Source: `docker-compose.full.yml:1-50`. Confidence: high.
- [X] OPS-005: The backend Dockerfile uses a multi-stage build: `mcr.microsoft.com/dotnet/sdk:9.0` (with `--platform=$BUILDPLATFORM` for cross-compile) restores `Waterblocks.Api.csproj`, copies `all_assets.json` into both `/src` and `/` (for runtime asset seeding), builds and publishes Release; runtime stage `mcr.microsoft.com/dotnet/aspnet:9.0` copies publish output, sets `ASPNETCORE_URLS=http://+:8080`, exposes 8080, entrypoint `dotnet Waterblocks.Api.dll`. Source: `Waterblocks.Api/Dockerfile:1-30`. Confidence: high.
- [X] OPS-006: The admin UI Dockerfile is a multi-stage Vite build: `node:20-alpine` (cross-compile via `BUILDPLATFORM`) runs `npm ci` then `npm run build` with `VITE_APP_COMMIT_HASH` build-arg embedded; runtime `nginx:1.27-alpine` installs `gettext` (for envsubst), serves `dist/` from `/usr/share/nginx/html`, copies `config.js.template` plus `docker-entrypoint.d/99-runtime-config.sh` (made executable) so nginx renders `config.js` from env at container start; uses `nginx.conf` as the only enabled site. Source: `waterblocks-admin/Dockerfile:1-18`. Confidence: high.
- [X] OPS-007: The admin nginx config serves SPA assets from `/usr/share/nginx/html` on port 80: long-cache (`max-age=31536000, immutable`) for static `.js/.css/.png/.jpg/.jpeg/.gif/.svg/.ico/.woff/.woff2`, `no-store` cache headers for `/config.js` (so runtime config refreshes), and a SPA fallback `try_files $uri $uri/ /index.html` with `no-store` on the index. Runtime API base URL is supplied via `config.js.template` (`window.__WB_CONFIG__ = { apiBaseUrl: "${API_BASE_URL}" }`) rendered by envsubst. Source: `waterblocks-admin/nginx.conf:1-22`, `waterblocks-admin/config.js.template:1-3`. Confidence: high.

## Cloud Deployment

- [X] OPS-008: `cloudformation/waterblocks-ecs.yml` provisions an AWS Fargate + RDS PostgreSQL + ALB stack with host-based routing for `ApiDomain` and `FrontendDomain` (HTTPS via ACM `CertificateArn`), pulling images from a configurable `DockerImageRegistryUrl` (default `ghcr.io/devnixs/waterblocks`) with `ApiImageTag`/`AdminImageTag` (default `latest`), RDS defaults to `db.t4g.micro` Postgres with `postgres`/`waterblocks` user/db, the Admin UI is protected by HTTP Basic Auth (`BasicAuthUsername`/`Password`), and the frontend's API base URL plus optional `DatadogApiKey` are passed through. Source: `cloudformation/waterblocks-ecs.yml:1-60`. Confidence: medium.

## CI/CD

- [X] OPS-009: `.github/workflows/ci.yml` (`CI`) runs on push/PR to `main` and `workflow_dispatch` on `ubuntu-latest` with a postgres:16-alpine service (port 5432, pg_isready healthcheck): checks out, sets up .NET 9.0.x, restores and builds `Waterblocks.Api.csproj` Release, runs `tests/backend/integration/Waterblocks.IntegrationTests.csproj` with trx logger (uploaded as `test-results` artifact), sets up Node 20 with npm cache, runs `npm ci` and `npm run build` for `waterblocks-admin` (with short-SHA `VITE_APP_COMMIT_HASH`), then builds multi-arch (linux/amd64,arm64) Docker images for both API and Admin UI via Buildx+QEMU with GHA layer cache, logging into `ghcr.io` and pushing to `ghcr.io/${repo}/api` and `/admin` only on push to main (tags: branch-sha, branch, pr, plus `latest` on default branch). Source: `.github/workflows/ci.yml:1-140`. Confidence: high.
- [X] OPS-010: `.github/workflows/claude.yml` (`Claude Code`) is the Anthropic Claude-Code bot integration: triggered by `issue_comment`/`pull_request_review_comment`/`issues`/`pull_request_review` when the body/title contains `@claude`; runs `anthropics/claude-code-action@v1` with `CLAUDE_CODE_OAUTH_TOKEN` secret and `actions: read` permission so Claude can read CI results on PRs. Source: `.github/workflows/claude.yml:1-50`. Confidence: high.

## Local Tooling

- [X] OPS-011: `run-compose.ps1` (PowerShell) and `run-compose.sh` (bash) are thin wrappers around `docker compose -f <file> <action> [extra args]`, dispatching `full|backend|frontend` to `docker-compose.full.yml`/`docker-compose.backend.yml`/`docker-compose.frontend.yml`; default action is `up` which is invoked with `-d` (detached), and any other action (e.g. `down`, `logs`) is passed through verbatim with remaining args (e.g. `--build`). Source: `run-compose.ps1:1-21`, `run-compose.sh:1-25`. Confidence: high.
