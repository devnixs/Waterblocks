# Story 02: Fee Selection UI Component

## Summary
Add a fee selection field to the transaction creation form that allows users to choose between Low, Medium, and High fee tiers.

## Background
When creating a transaction in the admin UI, users should be able to select the fee level. The fees should default to a sensible value (Medium tier) and display the estimated network fee for the selected asset.

## Acceptance Criteria

### AC1: Fee tier selector is displayed
- Add a fee tier selector below the amount field in `CreateTransactionForm.tsx`
- Display three options: Low, Medium, High
- Default selection: Medium

### AC2: Fee estimate is displayed
- Show the estimated network fee next to each tier option (or for the selected tier)
- Format: e.g., "Low - 0.00001 BTC" or "Medium - 0.0005 ETH"
- Display loading state while fetching estimates

### AC3: Fee tier selection is captured
- Store the selected fee tier in the form state
- Include the selected tier's `networkFee` value in the transaction creation request

### AC4: Allow manual fee override (optional enhancement)
- Optionally allow users to enter a custom fee value
- When custom is selected, show an input field for the fee amount

## UI Design

### Option A: Radio Buttons
```
Fee Level:
  ○ Low    (0.00001 BTC)
  ● Medium (0.000015 BTC) [default]
  ○ High   (0.000025 BTC)
```

### Option B: Dropdown with Descriptions
```
Fee Level: [Medium ▼]
  - Low: ~0.00001 BTC (slower confirmation)
  - Medium: ~0.000015 BTC (recommended)
  - High: ~0.000025 BTC (faster confirmation)
```

## Technical Notes

### Form State Changes
```typescript
// Add to form state
const [feeLevel, setFeeLevel] = useState<'LOW' | 'MEDIUM' | 'HIGH'>('MEDIUM');
const [feeEstimates, setFeeEstimates] = useState<EstimateFeeResponse | null>(null);
```

### Integration with React Query
```typescript
const { data: feeEstimates, isLoading: feesLoading } = useQuery({
  queryKey: ['estimateFee', selectedAssetId],
  queryFn: () => adminClient.estimateFee(selectedAssetId),
  enabled: !!selectedAssetId,
});
```

## Dependencies
- Story 01: Fee Estimation API must be accessible from frontend

## Out of Scope
- Gas price/gas limit inputs for ETH (advanced mode)
- Fee estimation for specific source/destination combinations
