# Waterblocks

Drop-in replacement for the Fireblocks API designed for testing crypto-trading platforms. It simulates blockchain operations against an in-database ledger and ships with an admin UI + admin API for deterministic testing.

## What’s inside
- **Fireblocks-compatible API**: endpoints matching `Fireblocks.swagger.yml`
- **Admin/Test API**: `{ data, error }` responses for UI + scripted tests
- **Admin UI**: real-time transaction visualization and control (SignalR)

## Stack
- **Backend**: ASP.NET Core (.NET 8) + EF Core + PostgreSQL
- **Frontend**: React + TypeScript (Vite)
- **Realtime**: SignalR WebSockets
- **Deployment**: Docker Compose

## Docker Images (GitHub Container Registry)

Pre-built Docker images are automatically published to GitHub Container Registry on every push to `main`:

- **API**: `ghcr.io/[owner]/[repo]/api:latest`
- **Admin UI**: `ghcr.io/[owner]/[repo]/admin:latest`

These images are publicly available and can be pulled without authentication. Tagged versions are also available using the commit SHA (e.g., `main-abc1234`).

### Using Published Images

You can use the published images directly in your `docker-compose.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: waterblocks
    ports:
      - "5432:5432"

  api:
    image: ghcr.io/[owner]/[repo]/api:latest
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=waterblocks;Username=postgres;Password=postgres"
    ports:
      - "5671:8080"
    depends_on:
      - postgres

  admin:
    image: ghcr.io/[owner]/[repo]/admin:latest
    environment:
      API_BASE_URL: "http://localhost:5671"
    ports:
      - "5173:80"
    depends_on:
      - api
```

### CI/CD Workflow

The `.github/workflows/ci.yml` workflow automatically:
1. Runs .NET and frontend builds
2. Executes integration tests
3. Builds Docker images for both API and Admin UI
4. Pushes images to GitHub Container Registry (only on `main` branch pushes)

Images are tagged with:
- `latest` (for the main branch)
- `main-<commit-sha>` (for specific commits)
- Branch names for feature branches (build-only, not pushed)

## AWS (CloudFormation)
The template `cloudformation/waterblocks-ecs.yml` provisions:
- VPC + ALB (HTTPS)
- ECS (Fargate)
- RDS PostgreSQL
- Basic auth in front of the Admin UI

Notes:
- Build and push `waterblocks-api:latest` and `waterblocks-admin:latest` images to your registry.
- The Admin UI image reads `API_BASE_URL` at runtime (generated into `/config.js`).
- Create DNS CNAMEs for `ApiDomain` and `FrontendDomain` pointing to the ALB DNS output.
- Task sizing is configurable via `FargateCpu`/`FargateMemory` (defaults to 0.25 vCPU / 0.5 GB).

### GitHub Actions deploy (ECR + ECS)
Workflow: `.github/workflows/deploy.yml` (runs on every push to `main`).

Set these in GitHub:
- **Secrets**: `AWS_ROLE_ARN`
- **Variables**: `AWS_REGION`, `CLOUDFORMATION_STACK`, `ECR_REPOSITORY_API`, `ECR_REPOSITORY_ADMIN`

## Quick start (Docker)
Use the helper scripts to run the correct compose file.

Full stack (API + UI + DB):
```bash
./run-compose.sh full up --build
```

Backend-only DB (useful for local API dev):
```bash
./run-compose.sh backend up
```

Frontend + API + DB:
```bash
./run-compose.sh frontend up --build
```

Windows PowerShell equivalents:
```powershell
./run-compose.ps1 -Stack full -Action up -- --build
```

### Ports
- **API**: `http://localhost:5671`
- **Admin UI**: `http://localhost:5173`
- **Postgres**: `localhost:5432`

## Key behaviors
- **Workspaces**: API keys, vaults, and transactions are workspace-scoped. Admin UI provides a workspace switcher and a workspaces page.
- **Idempotency**: `ExternalTxId` is unique (idempotency key). Duplicates return error code `1438`.
- **Assets**: seeds from `all_fireblocks_assets.json` at startup.
- **Realtime**: UI updates via SignalR; status indicator shows connection state.

## Configuration
- Backend uses `appsettings*.json` and environment variables.
- Admin UI reads `VITE_API_BASE_URL` from `waterblocks-admin/.env` (defaults to `http://localhost:5671`).

## Development (local)
Backend:
```bash
dotnet build

dotnet run --project Waterblocks.Api
```

Frontend:
```bash
cd waterblocks-admin
npm install
npm run dev
```

## Useful files
- `Fireblocks.swagger.yml`: Fireblocks API contract source of truth
- `SPECS.md` / `CLAUDE.md`: project rules and guidance
- `docker-compose.*.yml`: stack definitions

## Admin UI
- Transactions page: create + manage transactions and state transitions
- Vaults page: create vaults, create wallets, view balances + deposit addresses
- Workspaces page: create/delete/view workspaces and API keys

---

If you need anything specific added to the README (auth details, API examples, or CI usage), tell me what you want included.
