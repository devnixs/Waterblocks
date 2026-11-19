# AGENTS_API.md

Date: 2026-01-11

Work summary:
- Fixed Fireblocks-compatible routes for `/vault/accounts_paged` and `/vault/accounts/{vaultAccountId}/{assetId}/addresses_paginated`.
- Added missing `/vault/accounts/{vaultAccountId}/{assetId}/unspent_inputs` endpoint and DTOs.
- Enabled CORS for the admin UI origin (localhost:5173).
- Applied EF Core migrations on startup to ensure schema exists in containers.
- Added admin wallet creation endpoint with deposit address generation and exposed deposit address on vault responses.
- Extended admin transaction creation to support internal/external source/destination and ensured hashes are only generated on broadcast.
- Ensured wallet creation also generates a deposit address in the Fireblocks-compatible endpoint.
- Added SignalR hub and broadcast events for realtime admin updates.
- Broadcasts now include payloads for transaction/vault upserts to avoid refetches.
- Moved AdminHub definition into Program.cs to ensure it is tracked and compiled in CI.
- Added asset metadata columns and startup seeding from all_fireblocks_assets.json.
- Added admin settings storage and background auto-transition service.

Files touched:
- `Waterblocks.Api/Controllers/VaultAccountsController.cs`
- `Waterblocks.Api/Controllers/VaultAddressesController.cs`
- `Waterblocks.Api/Controllers/UnspentInputsController.cs`
- `Waterblocks.Api/Controllers/VaultWalletsController.cs`
- `Waterblocks.Api/Controllers/Admin/AdminVaultsController.cs`
- `Waterblocks.Api/Controllers/Admin/AdminTransactionsController.cs`
- `Waterblocks.Api/Dtos/Fireblocks/UnspentInputsDto.cs`
- `Waterblocks.Api/Dtos/Admin/AdminVaultDto.cs`
- `Waterblocks.Api/Dtos/Admin/AdminTransactionDto.cs`
- `Waterblocks.Api/Models/Transaction.cs`
- `Waterblocks.Api/Migrations/20260111094500_AddTransactionSources.cs`
- `Waterblocks.Api/Migrations/20260111094500_AddTransactionSources.Designer.cs`
- `Waterblocks.Api/Migrations/FireblocksDbContextModelSnapshot.cs`
- `Waterblocks.Api/Program.cs`
- `Waterblocks.Api/Waterblocks.Api.csproj`
- `Waterblocks.Api/Models/Asset.cs`
- `Waterblocks.Api/Infrastructure/Db/FireblocksDbContext.cs`
- `Waterblocks.Api/Migrations/20260111102500_AddAssetMetadata.cs`
- `Waterblocks.Api/Migrations/20260111102500_AddAssetMetadata.Designer.cs`
- `Waterblocks.Api/Models/AdminSetting.cs`
- `Waterblocks.Api/Dtos/Admin/AdminSettingsDto.cs`
- `Waterblocks.Api/Controllers/Admin/AdminSettingsController.cs`
- `Waterblocks.Api/Services/AutoTransitionService.cs`
- `Waterblocks.Api/Migrations/20260111110500_AddAdminSettings.cs`
- `Waterblocks.Api/Migrations/20260111110500_AddAdminSettings.Designer.cs`

Date: 2026-11-19

Work summary (STORY-021):
- Added Fireblocks-compatible `/exchange_accounts` and `/off_exchange` endpoints, and admin `/admin/exchange-accounts` endpoints.
- Added exchange accounts, collateral balance rows, Gate mirror events and idempotency records.
- `TransactionFactory` now holds the rules that `POST /transactions` and the collateral calls share.
- `TransactionStateService` is the shared path for a state change and its balance effects. A collateral transfer changes state in one database transaction that locks the transaction row.
- `ExchangeCollateralLedger` holds the exchange side of `ReserveFundsAsync`, `CompleteTransactionAsync` and `RollbackTransactionAsync`.
- `ExchangeMirrorWorker` sends pending events to the Gate simulator, oldest first, about once each second.
- `IdempotencyMiddleware` keeps the first reply of the three supported routes for 24 hours.
- `POST /transactions/{txId}/cancel` now releases reserved funds.
- `HttpTrafficLoggingMiddleware` now logs request bodies as `FND-005` says, and removes credentials on the exchange account routes.

Files touched:
- `Waterblocks.Api/Controllers/FireblocksCompatible/ExchangeAccountsController.cs`
- `Waterblocks.Api/Controllers/FireblocksCompatible/OffExchangeController.cs`
- `Waterblocks.Api/Controllers/FireblocksCompatible/TransactionsController.cs`
- `Waterblocks.Api/Controllers/Admin/AdminExchangeAccountsController.cs`
- `Waterblocks.Api/Services/` (exchange, collateral, mirror, state and transaction factory services)
- `Waterblocks.Api/Middleware/` (authentication, error mapper, idempotency, traffic logging)
- `Waterblocks.Api/Models/`, `Waterblocks.Api/Dtos/`, `Waterblocks.Api/Infrastructure/`
- `Waterblocks.Api/Migrations/20261118234357_AddOffExchangeCollateral.cs`
- `Fireblocks.swagger.yml`, `.env.example`, `docker-compose.yml`, `docker-compose.frontend.yml`, `docker-compose.full.yml`

Work summary (STORY-022):
- A workspace can have one `GATEIO` exchange account. A second one gets 409 with `GATE_ACCOUNT_ALREADY_EXISTS`.
- One Gate simulator account can be connected in only one workspace. A second connection gets 409 with `EXCHANGE_ACCOUNT_ALREADY_CONNECTED`.
- Two filtered unique indexes enforce the rules for parallel calls. `ExchangeAccountService` maps their violations to the same 409 replies.

Files touched:
- `Waterblocks.Api/Services/ExchangeAccountService.cs`
- `Waterblocks.Api/Models/ExchangeAccount.cs`
- `Waterblocks.Api/Infrastructure/Db/FireblocksDbContext.cs`
- `Waterblocks.Api/Migrations/20261119070824_OneGateAccountPerWorkspace.cs`
- `Fireblocks.swagger.yml`
