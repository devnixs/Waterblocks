# Story 03: Auto-Recompute Fees on Currency Change

## Summary
Automatically fetch and update fee estimates when the user changes the selected asset/currency in the transaction creation form.

## Background
Different blockchain assets have different fee structures and base fees. When the user selects a different asset, the displayed fee estimates must be refreshed to reflect the correct values for that asset.

## Acceptance Criteria

### AC1: Fees are fetched when asset is selected
- When the asset dropdown value changes, trigger a new `estimate_fee` API call
- Use React Query's automatic refetch based on query key change

### AC2: Loading state during fee fetch
- Show a loading indicator on the fee selector while fetching
- Disable fee selection during the loading state
- Display "Fetching fees..." or similar placeholder text

### AC3: Fees update after asset change
- Once the new estimates arrive, update the displayed fee values
- Maintain the user's selected fee tier (Low/Medium/High) if possible
- If the selected tier's fee changed, update the displayed value

### AC4: Handle missing fee data gracefully
- If an asset doesn't have a configured base fee, show "Fee unavailable"
- Allow form submission without a fee (backend will use defaults)

### AC5: Fee currency matches asset
- Display fees in the native asset currency
- Example: BTC transactions show fees in BTC, ETH in ETH
- Note: `FeeCurrency` field in model allows for different fee currencies (e.g., ETH gas fees for ERC-20 tokens)

## Technical Notes

### React Query Key Pattern
```typescript
// Query key includes assetId to auto-refetch on change
const { data, isLoading, error } = useQuery({
  queryKey: ['estimateFee', assetId],
  queryFn: () => adminClient.estimateFee(assetId),
  enabled: !!assetId,
  staleTime: 30000, // Cache for 30 seconds
});
```

### Asset Change Handler
```typescript
const handleAssetChange = (newAssetId: string) => {
  setAssetId(newAssetId);
  // React Query will automatically refetch due to queryKey change
  // No manual refetch needed
};
```

### Edge Cases
1. **User changes asset while fee fetch is in progress**
   - React Query handles this automatically (cancels stale requests)

2. **Asset has no base fee configured**
   - Backend returns zero fees
   - Frontend shows "No fee required" or similar

3. **Network error during fee fetch**
   - Show error message
   - Allow retry
   - Don't block form submission (fees are optional)

## Dependencies
- Story 01: Fee Estimation API
- Story 02: Fee Selection UI Component

## Out of Scope
- Caching fee estimates across sessions
- Real-time fee updates (polling)
- Fee estimates based on current network congestion
