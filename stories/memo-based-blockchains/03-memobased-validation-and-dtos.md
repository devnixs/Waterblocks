# Story 03: MemoBased Validation and API DTOs

## Summary
Require tags for MemoBased assets during validation and ensure tag fields are
returned in admin and Fireblocks-compatible responses.

## Background
Tag requirements are currently hardcoded to specific asset IDs. With
`BlockchainType.MemoBased`, validation should be based on asset type instead of
asset ID lists. The Admin API currently omits tags from its address DTOs.

## Acceptance Criteria

### AC1: Validation uses BlockchainType
- `RequiresTag()` uses `asset.BlockchainType == MemoBased`.
- Transaction creation fails when a MemoBased asset is used without a tag.

### AC2: Admin API includes tag
- `AdminAddressDto` exposes a `tag` field.
- Admin address responses populate `tag` for MemoBased addresses.

### AC3: Fireblocks-compatible responses surface tags
- Vault address creation and listing include `Tag` where present.
- Existing response shapes remain unchanged (no breaking changes).

## Technical Notes
- Target files:
  - `Waterblocks.Api/Services/AddressValidationService.cs`
  - `Waterblocks.Api/Dtos/Admin/AdminAddressDto.cs`
  - `Waterblocks.Api/Controllers/FireblocksCompatible/VaultAddressesController.cs`
  - `Waterblocks.Api/Controllers/FireblocksCompatible/VaultWalletsController.cs`

## Dependencies
- Story 02 (tag persistence)

## Out of Scope
- Frontend UI changes
