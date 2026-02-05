# Story 05: Admin UI Transaction Tag Input

## Summary
Allow users to specify a memo/tag when creating transactions in the Admin UI
for MemoBased assets.

## Background
MemoBased blockchains require a tag to route funds to the intended recipient.
The backend already accepts `destinationTag`, but the Admin UI does not expose
an input for it.

## Acceptance Criteria

### AC1: UI shows tag input for MemoBased assets
- The transaction creation form displays a tag/memo input when the selected
  asset is MemoBased.
- The input is hidden or disabled for non-MemoBased assets.

### AC2: Tag is sent in create transaction request
- The Admin UI includes `destinationTag` in the create-transaction payload when
  the user provides a tag.
- Empty or missing tags are not sent for non-MemoBased assets.

### AC3: UX aligns with existing form behavior
- The new input follows existing validation and layout patterns.
- The field label clarifies it is a tag/memo for MemoBased assets.

## Technical Notes
- Target files:
  - `waterblocks-admin/src/pages/transactions/CreateTransactionForm.tsx`
  - `waterblocks-admin/src/types/admin.ts`
  - `waterblocks-admin/src/api/adminClient.ts`
- Consider using `asset.blockchainType` to toggle the input.

## Dependencies
- Story 03 (MemoBased validation and DTOs)

## Out of Scope
- Backend validation changes beyond existing `destinationTag` handling
