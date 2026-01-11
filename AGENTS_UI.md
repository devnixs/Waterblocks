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

Files touched:
- `fireblocksreplacement-admin/src/api/adminClient.ts`
- `fireblocksreplacement-admin/src/api/queries.ts`
- `fireblocksreplacement-admin/src/pages/TransactionsPage.tsx`
- `fireblocksreplacement-admin/src/pages/VaultsPage.tsx`
- `fireblocksreplacement-admin/src/types/admin.ts`
- `fireblocksreplacement-admin/src/App.css`
- `fireblocksreplacement-admin/src/components/ToastProvider.tsx`
- `fireblocksreplacement-admin/src/hooks/useRealtimeUpdates.ts`
- `fireblocksreplacement-admin/src/App.tsx`
- `fireblocksreplacement-admin/package.json`
- `fireblocksreplacement-admin/package-lock.json`
