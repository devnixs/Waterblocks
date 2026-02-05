# Story 04: Admin UI Display of Memo/Tag

## Summary
Show memo/tag values in the vault wallets view for MemoBased assets.

## Background
The Admin UI currently lists wallet addresses but does not display tags. For
MemoBased assets, tags are essential for identifying unique wallets that share
the same address.

## Acceptance Criteria

### AC1: Frontend types include tag
- `AdminAddress` (or equivalent) includes an optional `tag` field.
- Types align with the Admin API response.

### AC2: Vault wallets view shows tag
- `VaultWalletsSection` displays the tag when present.
- The display is unobtrusive (e.g., an extra column or a subtitle line).
- Non-MemoBased assets remain unchanged.

## Technical Notes
- Target files:
  - `waterblocks-admin/src/types/admin.ts`
  - `waterblocks-admin/src/pages/vaults/VaultWalletsSection.tsx`

## Dependencies
- Story 03 (Admin API DTOs include tag)

## Out of Scope
- UI changes to transaction forms or filters
