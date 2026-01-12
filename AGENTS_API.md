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
