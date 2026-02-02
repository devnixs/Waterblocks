# Story 01: Fee Estimation API for Admin UI

## Summary
Expose fee estimation functionality to the admin UI so users can see estimated fees before creating a transaction.

## Background
The Fireblocks-compatible API already has an `estimate_fee` endpoint at `POST /transactions/estimate_fee` that returns Low/Medium/High fee tiers based on the asset's base fee. The admin UI needs to consume this endpoint to display fee estimates.

## Acceptance Criteria

### AC1: Admin UI can call the estimate_fee endpoint
- The frontend should be able to call `POST /transactions/estimate_fee` with the selected asset
- Request payload should include:
  - `assetId` (required)
  - `amount` (optional, can default to "0")
  - `source` and `destination` (optional, can use empty defaults)

### AC2: Response includes fee tiers
- The endpoint returns three fee tiers: `low`, `medium`, `high`
- Each tier includes:
  - `networkFee` - the total estimated fee
  - `feePerByte` (for BTC/ADA)
  - `gasPrice`, `gasLimit`, `baseFee`, `priorityFee` (for ETH-based assets)

### AC3: Add API client method in frontend
- Add `estimateFee(assetId: string)` method to `adminClient.ts`
- Method should call the Fireblocks-compatible estimate_fee endpoint
- Return typed response with Low/Medium/High fee estimates

## Technical Notes

### Existing Implementation
The endpoint is already implemented in `TransactionsController.cs` (lines 253-352):
- Uses `asset.BaseFee` as the base value
- Multipliers: Low=1.0x, Medium=1.5x, High=2.5x
- Asset-specific handling for BTC, ETH, SOL, ADA

### API Endpoint
```
POST /transactions/estimate_fee
Content-Type: application/json

{
  "assetId": "BTC",
  "amount": "0",
  "source": { "type": "VAULT_ACCOUNT" },
  "destination": { "type": "ONE_TIME_ADDRESS" }
}
```

### Response Shape
```typescript
interface EstimateFeeResponse {
  low: FeeEstimate;
  medium: FeeEstimate;
  high: FeeEstimate;
}

interface FeeEstimate {
  networkFee?: string;
  feePerByte?: string;
  gasPrice?: string;
  gasLimit?: string;
  baseFee?: string;
  priorityFee?: string;
}
```

## Dependencies
- None (endpoint already exists)

## Out of Scope
- Changing the fee calculation logic
- Adding new fee tiers
