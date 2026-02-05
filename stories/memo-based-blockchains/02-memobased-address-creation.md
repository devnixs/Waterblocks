# Story 02: MemoBased Address Creation and Uniqueness

## Summary
For MemoBased assets, reuse a single address value per vault+asset and generate
a new unique memo/tag for each address creation.

## Background
MemoBased blockchains (e.g., XRP/XLM) route funds using a shared address and a
memo/tag. We should treat MemoBased assets as address-reuse, but generate unique
tags for each new address record created in the vault.

## Acceptance Criteria

### AC1: Reuse primary address value
- For MemoBased assets, the address value is reused from the primary address
  for the vault+asset.
- A new `Address` record is still created when `CreateAddress` is called.

### AC2: Unique tag per vault+asset
- Each new `Address` record for a MemoBased asset receives a unique tag.
- Uniqueness is enforced per vault+asset.
- On collision, the service retries tag generation (with a bounded retry count).

### AC3: Tag is persisted with address
- The `Address.Tag` field is populated for MemoBased assets.
- Existing AccountBased and AddressBased behaviors remain unchanged.

## Technical Notes
- Target file: `Waterblocks.Api/Services/WalletAddressService.cs`
- Reuse existing primary address discovery logic to keep address value stable.
- Uniqueness check should query addresses scoped to the same vault+asset.

## Dependencies
- Story 01 (tag generation in `AddressGenerator`)

## Out of Scope
- Database-level uniqueness constraints or migrations
- UI display changes
