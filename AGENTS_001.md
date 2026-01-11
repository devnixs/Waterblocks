# AGENTS_001.md

Date: 2026-01-11

Work summary:
- Fixed Fireblocks-compatible routes for `/vault/accounts_paged` and `/vault/accounts/{vaultAccountId}/{assetId}/addresses_paginated`.
- Added missing `/vault/accounts/{vaultAccountId}/{assetId}/unspent_inputs` endpoint and DTOs.
- Implemented admin UI transaction filters (asset, transaction ID, hash) and default sort by created date descending.
- Added frozen balances fetching and display to the admin UI vault detail panel.
- Added tests directory scaffolding under `tests/` with placeholder files.

Files touched:
- `FireblocksReplacement.Api/Controllers/VaultAccountsController.cs`
- `FireblocksReplacement.Api/Controllers/VaultAddressesController.cs`
- `FireblocksReplacement.Api/Controllers/UnspentInputsController.cs`
- `FireblocksReplacement.Api/Dtos/Fireblocks/UnspentInputsDto.cs`
- `fireblocksreplacement-admin/src/api/adminClient.ts`
- `fireblocksreplacement-admin/src/api/queries.ts`
- `fireblocksreplacement-admin/src/pages/TransactionsPage.tsx`
- `fireblocksreplacement-admin/src/pages/VaultsPage.tsx`
- `fireblocksreplacement-admin/src/types/admin.ts`
- `fireblocksreplacement-admin/src/App.css`
- `tests/backend/unit/.gitkeep`
- `tests/backend/integration/.gitkeep`
- `tests/frontend/unit/.gitkeep`
- `tests/frontend/e2e/.gitkeep`
- `AGENTS.md`
