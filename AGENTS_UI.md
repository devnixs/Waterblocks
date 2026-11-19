# AGENTS_UI.md

Date: 2026-01-11

Work summary:
- Implemented admin UI transaction filters (asset, transaction ID, hash) and default sort by created date descending.
- Added frozen balances fetching and display to the admin UI vault detail panel.
- Fixed React type-only import for TypeScript verbatimModuleSyntax.
- Added wallet creation form per vault with deposit address display.
- Added blockchain transaction creation form with internal/external source/destination selection.
- Enabled Enter-to-submit behavior on admin forms.
- Replaced alert() usage with toast notifications to avoid blocking UI automation.
- Synced selected vault detail panel with latest vault list to avoid stale wallet counts/addresses.
- Added SignalR client hook to invalidate queries on realtime updates.
- Added realtime connection status badge and cache upserts on websocket payloads.
- Added client-side paging controls for the transactions list.
- Added auto-transition toggle in the header.

Files touched:
- `waterblocks-admin/src/api/adminClient.ts`
- `waterblocks-admin/src/api/queries.ts`
- `waterblocks-admin/src/pages/TransactionsPage.tsx`
- `waterblocks-admin/src/pages/VaultsPage.tsx`
- `waterblocks-admin/src/types/admin.ts`
- `waterblocks-admin/src/App.css`
- `waterblocks-admin/src/components/ToastProvider.tsx`
- `waterblocks-admin/src/hooks/useRealtimeUpdates.ts`
- `waterblocks-admin/src/App.tsx`
- `waterblocks-admin/package.json`
- `waterblocks-admin/package-lock.json`

Date: 2026-11-19

Work summary (STORY-021):
- Added the Exchanges page, the connect dialog, the collateral vault account screen, the collateral transfer dialog and the transfers list.
- Added the `Exchanges` nav link and keyboard shortcut `5`.
- The connect dialog calls `adminApi` directly, so that credentials stay out of React Query. Its state ends when the dialog closes.
- The Transactions table and detail panel show the exchange account name on the exchange side of a collateral transfer.
- Moved the amount summary of the transaction form to `TransactionAmountSummary`, so that the transfer dialog uses the same summary.

Files touched:
- `waterblocks-admin/src/App.tsx`, `waterblocks-admin/src/App.css`
- `waterblocks-admin/src/api/adminClient.ts`, `waterblocks-admin/src/api/queries.ts`
- `waterblocks-admin/src/hooks/useRealtimeUpdates.ts`, `waterblocks-admin/src/types/admin.ts`
- `waterblocks-admin/src/components/KeyboardShortcutsDialog.tsx`
- `waterblocks-admin/src/pages/ExchangesPage.tsx`, `waterblocks-admin/src/pages/ExchangeAccountPage.tsx`
- `waterblocks-admin/src/pages/exchanges/`
- `waterblocks-admin/src/pages/transactions/` (form, table, detail panel, amount summary)

Work summary (STORY-022):
- The Exchanges page shows `A workspace can have only one Gate account.` in place of `Connect account` when the workspace has a Gate account.

Files touched:
- `waterblocks-admin/src/pages/ExchangesPage.tsx`
