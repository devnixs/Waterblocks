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
- **Assets**: seeds from `all_assets.json` at startup.
- **Realtime**: UI updates via SignalR; status indicator shows connection state.
- **Off Exchange**: exchange accounts, Add collateral and Remove collateral work through the API and the Exchanges screens.

## Off Exchange and exchange simulators
Waterblocks serves these Fireblocks-compatible calls, without `/v1`:

- `POST`, `GET /exchange_accounts`, `GET /exchange_accounts/paged`, `GET /exchange_accounts/credentials_public_key`
- `GET /exchange_accounts/{exchangeAccountId}`, `GET /exchange_accounts/{exchangeAccountId}/{assetId}`
- `POST /off_exchange/add`, `POST /off_exchange/remove`, `GET /off_exchange/collateral_accounts/{mainExchangeAccountId}`

`POST /exchange_accounts`, `POST /off_exchange/add` and `POST /off_exchange/remove` accept the `Idempotency-Key` header. A key has at most 40 characters. For 24 hours, a repeated key returns the first reply.

An exchange account can connect to an account in the simulator of its exchange. Each exchange that Waterblocks can connect has one simulator client. Today the only client is the Gate.io simulator (exchange type `GATEIO`). `GET /admin/exchange-providers` lists the clients, their assets and their rules. The Admin UI reads this list.

Each exchange has its own settings, with the exchange type in the name:

| Setting | Meaning |
| --- | --- |
| `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__BASE_URL` | Simulator origin, for example `EXCHANGE_SIMULATORS__GATEIO__BASE_URL=http://host.docker.internal:8083`. An empty value turns the connection off |
| `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__ADMIN_TOKEN` | Optional. The Gate.io client sends it as `X-Admin-Token` on the simulator `/_api` calls |

Open **Exchanges** in the Admin UI, select **Connect account**, select the exchange, and enter an API key and secret of the simulator account. Waterblocks uses the secret only for the connection check. It does not store the secret.

A simulator account can be connected in only one workspace. An exchange can allow only one account in each workspace. Gate.io uses this rule.

For a connected account, one background worker sends collateral events to the simulator of the account:

- An add sends its net amount when the transaction reaches `COMPLETED`.
- A removal sends the amount that leaves the collateral vault when it is created, and waits for the simulator to approve it.
- A removal that fails after approval sends a reversal.

To add an exchange, for example Bybit:

1. Add a class in `Waterblocks.Api/Services/ExchangeSimulators` that extends `ExchangeSimulatorClientBase` (or implements `IExchangeSimulatorClient`). Set the exchange type, the display name, the assets and the one-account rule, and write the calls of that simulator. Map each Waterblocks asset id to the currency name of the exchange inside the client. For example, the Gate.io client maps `USDT_ERC20` to `USDT`.
2. Register it in `Program.cs` with `builder.Services.AddExchangeSimulatorClient<TheNewClient>();`.
3. Set `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__BASE_URL`.

The tables, the API and the Admin UI need no change.

## Configuration
- Backend uses `appsettings*.json` and environment variables.
- Set `DATADOG_API_KEY` to enable Datadog log shipping from the API. Optional overrides: `Datadog__Url`, `Datadog__Service`, `Datadog__Source`, and `Datadog__Tags__0`.
- Optional runtime workspace seeding is supported via environment variables:
  - `SEED_WORKSPACE_1_NAME=Hot`
  - `SEED_WORKSPACE_1_APIKEY=XXXX`
  - Add more workspaces by incrementing the index (`SEED_WORKSPACE_2_*`, `SEED_WORKSPACE_3_*`, ...).
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
- Exchanges page: connect an exchange simulator account, view collateral balances, add and remove collateral
- Workspaces page: create/delete/view workspaces and API keys

---

If you need anything specific added to the README (auth details, API examples, or CI usage), tell me what you want included.
