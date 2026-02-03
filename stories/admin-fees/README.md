# Admin Transaction Fees Feature

## Overview
This feature adds the ability to select and configure transaction fees when creating transactions through the admin UI.

## User Value
- **Realistic testing**: Test systems can create transactions with specific fee configurations that mirror production scenarios
- **Fee deduction testing**: Verify how your application handles gross vs net amount calculations
- **Asset-specific fees**: Each currency has appropriate default fees

## Stories

| # | Story | Description | Effort |
|---|-------|-------------|--------|
| 01 | [Fee Estimation API](./01-fee-estimation-api.md) | Add API client method to call existing estimate_fee endpoint | Small |
| 02 | [Fee Selection UI](./02-fee-selection-ui.md) | Add fee tier selector (Low/Medium/High) to create form | Medium |
| 03 | [Auto-Recompute Fees](./03-auto-recompute-fees.md) | Refresh fees when asset changes | Small |
| 04 | [Fee Deduction Mode](./04-fee-deduction-mode.md) | Toggle for deducting fees from amount | Medium |
| 05 | [Backend Accept Fees](./05-backend-accept-fees.md) | Update admin API to accept fee fields | Medium |

## Implementation Order

```
┌─────────────────────────────────────────────────────────────┐
│  Phase 1: Backend Foundation                                │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Story 05: Backend Accept Fees                        │   │
│  │ - Add fields to CreateAdminTransactionRequestDto     │   │
│  │ - Wire fee storage in controller                     │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  Phase 2: Frontend API Integration                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Story 01: Fee Estimation API                         │   │
│  │ - Add estimateFee() to adminClient.ts                │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  Phase 3: UI Components                                     │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Story 02: Fee Selection UI                           │   │
│  │ - Add fee tier radio buttons                         │   │
│  │ - Display estimated fees                             │   │
│  └─────────────────────────────────────────────────────┘   │
│                          │                                  │
│                          ▼                                  │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Story 03: Auto-Recompute Fees                        │   │
│  │ - Trigger refetch on asset change                    │   │
│  │ - Handle loading states                              │   │
│  └─────────────────────────────────────────────────────┘   │
│                          │                                  │
│                          ▼                                  │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Story 04: Fee Deduction Mode                         │   │
│  │ - Add treatAsGrossAmount toggle                      │   │
│  │ - Show recipient amount preview                      │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Key Files to Modify

### Backend
- `Waterblocks.Api/Controllers/Admin/AdminTransactionsController.cs`
- `Waterblocks.Api/Dtos/Admin/CreateAdminTransactionRequestDto.cs`

### Frontend
- `waterblocks-admin/src/api/adminClient.ts`
- `waterblocks-admin/src/pages/transactions/CreateTransactionForm.tsx`

## Existing Infrastructure

### Already Implemented
- `POST /transactions/estimate_fee` endpoint (Fireblocks-compatible)
- `Transaction.Fee`, `Transaction.NetworkFee`, `Transaction.TreatAsGrossAmount` model fields
- `AdminTransactionDto` includes fee fields in responses

### Fireblocks Swagger Reference
The fee fields follow the Fireblocks API specification:
- `feeLevel`: "LOW" | "MEDIUM" | "HIGH"
- `fee`: Explicit fee amount
- `treatAsGrossAmount`: Boolean for fee deduction mode
- Response includes `networkFee`, `feePerByte`, `gasPrice`, etc.

## Testing Considerations

1. **Different asset types**: Test fee estimation for BTC, ETH, SOL, ERC-20 tokens
2. **Fee deduction math**: Verify recipient amount calculation with gross mode
3. **Edge cases**: Zero fees, missing base fees, asset changes mid-form
4. **Form validation**: Ensure fee is included in submission
