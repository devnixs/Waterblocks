---
area: routing
owners:
  - frontend
status: active
depends_on:
  - specs/00-foundations.spec.md
---

## Routes

- [X] UI-TXN-001: The admin UI registers `/` and `/transactions` to render `TransactionsPage`, which lists workspace transactions (paginated via `useTransactionsPaged`), supports asset/id/hash filters, a create-transaction form, bulk selection/actions, keyboard navigation, and a detail panel with state transitions. Source: `waterblocks-admin/src/App.tsx:183-189`, `waterblocks-admin/src/pages/TransactionsPage.tsx:17-644`. Confidence: high.
- [X] UI-VAULT-001: The admin UI registers `/vaults` to render `VaultsPage`, which lists workspace vaults (`useVaults`), can toggle archived view, supports a create-vault form, opens a detail panel with wallet creation/rename/archive/unarchive, and honors `?vaultId=`/`?vaultName=` query parameters for auto-selection. Source: `waterblocks-admin/src/App.tsx:186`, `waterblocks-admin/src/pages/VaultsPage.tsx:20-192`. Confidence: high.
- [X] UI-WS-001: The admin UI registers `/workspaces` to render `WorkspacesPage`, which lists all workspaces (`useWorkspaces`), creates new workspaces with an `autoTransitionEnabled` toggle, archives them via `useDeleteWorkspace`, and displays each workspace's API keys. Source: `waterblocks-admin/src/App.tsx:187`, `waterblocks-admin/src/pages/WorkspacesPage.tsx:5-138`. Confidence: high.
- [X] UI-ASSET-001: The admin UI registers `/assets` to render `AssetsPage`, which lists assets (`useAdminAssets`), creates assets with full metadata (assetId/name/symbol/decimals/type/blockchainType/contract/native/baseFee/feeAssetId/isCaseSensitive/isActive), and edits/deactivates via an `AssetEditPanel`. Source: `waterblocks-admin/src/App.tsx:188`, `waterblocks-admin/src/pages/AssetsPage.tsx:24-310`. Confidence: high.

## App Shell

- [X] UI-SHELL-001: The app shell renders a header with logo, build commit hash (from `VITE_APP_COMMIT_HASH`), nav links to Transactions/Vaults/Workspaces/Assets (active class based on `location.pathname`), a workspace selector `<select>` that persists `workspaceId` to `localStorage` and invalidates all queries on change, a `?` help button, an Auto-transition toggle (calls `useSetAutoTransitions`, disabled while pending or without workspace), a realtime status indicator driven by `useRealtimeUpdates`, and a user info block with current email and Logout button. Source: `waterblocks-admin/src/App.tsx:30-181`. Confidence: high.
- [X] UI-SHELL-002: The app shell auto-selects the first workspace when none is stored, and falls back to the first workspace if the stored `workspaceId` no longer exists in the workspace list. Source: `waterblocks-admin/src/App.tsx:57-78`. Confidence: high.
- [X] UI-SHELL-003: The app shell gates the entire UI behind `LoginGate` (rendered when `useCurrentUser` reports no email) and only mounts the routed shell once an email is captured. Source: `waterblocks-admin/src/App.tsx:88-90`. Confidence: high.
- [X] UI-SHELL-004: The app shell binds global keyboard shortcuts via `useKeyboardShortcuts`: `1`/`2`/`3`/`4` navigate to Transactions/Vaults/Workspaces/Assets, and `?` opens the keyboard shortcuts dialog. Source: `waterblocks-admin/src/App.tsx:80-86`. Confidence: high.
- [X] UI-SHELL-005: The `KeyboardShortcutsDialog` (Radix Dialog) documents Global shortcuts (1-4 nav, `/` focus, `Esc` close, `?` help), List navigation (`j`/`k`/arrows, `Enter`, `Space`, `Ctrl/Cmd+A`/`D`), and Transaction Detail Panel shortcuts (`a`/`s`/`c`/`f`/`x` actions, `Ctrl/Cmd+C` copy), and is opened from the shell's `?` button or `?` key. Source: `waterblocks-admin/src/components/KeyboardShortcutsDialog.tsx:8-95`. Confidence: high.
- [X] UI-SHELL-006: The `useKeyboardShortcuts` hook attaches a `window` keydown listener that matches key, ctrl/meta, and shift modifiers (treating Ctrl and Cmd as equivalent), suppresses shortcuts while focus is in INPUT/TEXTAREA/contentEditable (except Escape), calls `preventDefault()` before invoking the handler, and short-circuits after the first match; the entire binding can be toggled via the `enabled` flag. Source: `waterblocks-admin/src/hooks/useKeyboardShortcuts.ts:12-45`. Confidence: high.

## Searchable Vault Select

- [X] UI-VAULT-002: The `SearchableVaultSelect` component renders a dropdown that filters vaults by case-insensitive substring match against vault name, vault id, wallet `depositAddress`, or any wallet address value (scoped to a specific `assetId` when provided); shows the selected vault as a static display until clicked, supports clearing the selection via an `x` button, and closes on outside-click. Source: `waterblocks-admin/src/components/SearchableVaultSelect.tsx:12-164`. Confidence: high.
