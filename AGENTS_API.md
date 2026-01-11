# AGENTS_API.md

Date: 2026-01-11

Work summary:
- Fixed Fireblocks-compatible routes for `/vault/accounts_paged` and `/vault/accounts/{vaultAccountId}/{assetId}/addresses_paginated`.
- Added missing `/vault/accounts/{vaultAccountId}/{assetId}/unspent_inputs` endpoint and DTOs.

Files touched:
- `FireblocksReplacement.Api/Controllers/VaultAccountsController.cs`
- `FireblocksReplacement.Api/Controllers/VaultAddressesController.cs`
- `FireblocksReplacement.Api/Controllers/UnspentInputsController.cs`
- `FireblocksReplacement.Api/Dtos/Fireblocks/UnspentInputsDto.cs`
