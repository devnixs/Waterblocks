# Story 01: Memo/Tag Generation for MemoBased Assets

## Summary
Add alphanumeric memo/tag generation to the address generator so MemoBased
assets can create unique tags for each address creation.

## Background
The backend already supports `MemoBased` in the `BlockchainType` enum and has a
`Tag` field on `Address`, but tags are never generated. We need a consistent,
alphanumeric tag generator and a way to return the tag to callers.

## Acceptance Criteria

### AC1: Address generator produces alphanumeric tags
- Add a memo/tag generator in `AddressGenerator`.
- Tags are alphanumeric (A–Z, a–z, 0–9).
- Tags have a fixed length (default 12 characters).

### AC2: Tag generation is exposed to callers
- `AddressGenerationResult` (or equivalent) includes the generated tag.
- Callers can retrieve the tag when generating new addresses for MemoBased assets.

## Technical Notes
- Target file: `Waterblocks.Api/Services/AddressGenerator.cs`
- If `AddressGenerationResult` is extended, update its call sites.
- Length can be adjusted later if product needs change; keep a single constant.

## Dependencies
- None

## Out of Scope
- Tag uniqueness enforcement
- Validation of tag format on inputs
