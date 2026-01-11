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

Files touched:
- `FireblocksReplacement.Api/Controllers/VaultAccountsController.cs`
- `FireblocksReplacement.Api/Controllers/VaultAddressesController.cs`
- `FireblocksReplacement.Api/Controllers/UnspentInputsController.cs`
- `FireblocksReplacement.Api/Controllers/VaultWalletsController.cs`
- `FireblocksReplacement.Api/Controllers/Admin/AdminVaultsController.cs`
- `FireblocksReplacement.Api/Controllers/Admin/AdminTransactionsController.cs`
- `FireblocksReplacement.Api/Dtos/Fireblocks/UnspentInputsDto.cs`
- `FireblocksReplacement.Api/Dtos/Admin/AdminVaultDto.cs`
- `FireblocksReplacement.Api/Dtos/Admin/AdminTransactionDto.cs`
- `FireblocksReplacement.Api/Models/Transaction.cs`
- `FireblocksReplacement.Api/Migrations/20260111094500_AddTransactionSources.cs`
- `FireblocksReplacement.Api/Migrations/20260111094500_AddTransactionSources.Designer.cs`
- `FireblocksReplacement.Api/Migrations/FireblocksDbContextModelSnapshot.cs`
- `FireblocksReplacement.Api/Hubs/AdminHub.cs`
- `FireblocksReplacement.Api/Program.cs`
