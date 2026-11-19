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
- Added exchange accounts, collateral balance rows, mirror events and idempotency records.
- `TransactionFactory` now holds the rules that `POST /transactions` and the collateral calls share.
- `TransactionStateService` is the shared path for a state change and its balance effects. A collateral transfer changes state in one database transaction that locks the transaction row.
- `ExchangeCollateralLedger` holds the exchange side of `ReserveFundsAsync`, `CompleteTransactionAsync` and `RollbackTransactionAsync`.
- `ExchangeMirrorWorker` sends pending events to the exchange simulators, oldest first, about once each second.
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
- `Waterblocks.Api/Migrations/20261119204326_AddOffExchangeCollateral.cs`
- `Fireblocks.swagger.yml`, `.env.example`, `docker-compose.yml`, `docker-compose.frontend.yml`, `docker-compose.full.yml`

Work summary (STORY-022):
- An exchange can allow one account in each workspace. Gate.io uses this rule. A second account gets 409 with `EXCHANGE_ACCOUNT_LIMIT_REACHED`.
- A simulator account of an exchange type can be connected in only one workspace. A second connection gets 409 with `EXCHANGE_ACCOUNT_ALREADY_CONNECTED`.
- A database lock on the workspace and the exchange type, and a filtered unique index on the exchange type and the simulator account id, make parallel calls safe.

Files touched:
- `Waterblocks.Api/Services/ExchangeAccountService.cs`
- `Waterblocks.Api/Models/ExchangeAccount.cs`
- `Waterblocks.Api/Infrastructure/Db/FireblocksDbContext.cs`
- `Fireblocks.swagger.yml`

Work summary (STORY-023):
- Added `IExchangeSimulatorClient`, `ExchangeSimulatorClientBase`, `IExchangeSimulatorRegistry` and `AddExchangeSimulatorClient<T>()`. Each exchange that Waterblocks can connect has one client. `GateSimulatorClient` is the first client and holds all Gate.io calls.
- The services, the mirror worker and the collateral checks ask the registry for the client of the exchange type of the account. No other code names an exchange.
- Renamed the connection columns to `SimulatorAccountId` and `SimulatorAccountName`, and added `ExchangeType` to the mirror events. One migration, `AddOffExchangeCollateral`, replaces the two earlier migrations of this branch.
- Renamed the error codes to `EXCHANGE_SIMULATOR_NOT_CONFIGURED`, `EXCHANGE_SIMULATOR_UNREACHABLE`, `SIMULATOR_ACCOUNT_NOT_FOUND` and `EXCHANGE_ACCOUNT_LIMIT_REACHED`, and the settings to `EXCHANGE_SIMULATORS__<EXCHANGE_TYPE>__BASE_URL` and `__ADMIN_TOKEN`.
- Added `GET /admin/exchange-providers`.
- `POST /exchange_accounts` also accepts the exchange type of each simulator client, so that a new exchange outside the Fireblocks list of that call needs no second change.

Files touched:
- `Waterblocks.Api/Services/ExchangeSimulators/`
- `Waterblocks.Api/Services/ExchangeAccountService.cs`, `CollateralTransferService.cs`, `ExchangeCollateralLedger.cs`, `ExchangeMirrorProcessor.cs`, `ExchangeMirrorWorker.cs`
- `Waterblocks.Api/Controllers/Admin/AdminExchangeProvidersController.cs`
- `Waterblocks.Api/Models/ExchangeAccount.cs`, `ExchangeMirrorEvent.cs`, `ExchangeTypes.cs`, `Transaction.cs`
- `Waterblocks.Api/Dtos/Admin/AdminExchangeAccountDto.cs`
- `Waterblocks.Api/Infrastructure/Db/FireblocksDbContext.cs`
- `Waterblocks.Api/Migrations/20261119204326_AddOffExchangeCollateral.cs`, `FireblocksDbContextModelSnapshot.cs`
- `Waterblocks.Api/Program.cs`
- `README.md`, `Fireblocks.swagger.yml`, `.env.example`, `docker-compose.yml`, `docker-compose.frontend.yml`, `docker-compose.full.yml`
