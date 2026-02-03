# Story 05: Backend - Accept Fee Fields in Admin Transaction Creation

## Summary
Update the admin transaction creation endpoint to accept fee-related fields so that transactions can be created with specific fee configurations.

## Background
The Transaction model already has fee fields (`Fee`, `NetworkFee`, `ServiceFee`, `FeeCurrency`, `TreatAsGrossAmount`), but the admin API's `CreateAdminTransactionRequestDto` doesn't expose them. This story adds the necessary fields to the DTO and wires them to the transaction creation logic.

## Acceptance Criteria

### AC1: DTO accepts fee fields
- Add the following fields to `CreateAdminTransactionRequestDto`:
  - `NetworkFee` (string, optional) - The network fee amount
  - `FeeLevel` (string, optional) - "LOW", "MEDIUM", or "HIGH"
  - `TreatAsGrossAmount` (bool, optional, default: false)

### AC2: Fee is stored in transaction
- When `NetworkFee` is provided, store it in `Transaction.NetworkFee`
- When `FeeLevel` is provided without `NetworkFee`, calculate fee from asset's base fee
- Store `TreatAsGrossAmount` value in the transaction entity

### AC3: Fee currency is determined automatically
- Set `Transaction.FeeCurrency` based on the asset type:
  - Native assets (BTC, ETH, SOL): fee currency = asset ID
  - Tokens (ERC-20, etc.): fee currency = parent chain asset (e.g., ETH for USDC)

### AC4: Response includes fee information
- `AdminTransactionDto` already includes `Fee` and `NetworkFee` fields
- Ensure the response reflects the stored fee values

## Technical Notes

### DTO Changes
```csharp
// In CreateAdminTransactionRequestDto.cs
public class CreateAdminTransactionRequestDto
{
    // ... existing fields ...

    /// <summary>
    /// Network fee amount. If not provided, uses the default for the fee level.
    /// </summary>
    public string? NetworkFee { get; set; }

    /// <summary>
    /// Fee level: LOW, MEDIUM, or HIGH. Used to calculate fee if NetworkFee not provided.
    /// </summary>
    public string? FeeLevel { get; set; }

    /// <summary>
    /// If true, the fee is deducted from the amount. If false, fee is added to amount.
    /// </summary>
    public bool TreatAsGrossAmount { get; set; } = false;
}
```

### Service Logic
```csharp
// In AdminTransactionsController or TransactionService

// Determine network fee
decimal networkFee;
if (!string.IsNullOrEmpty(request.NetworkFee))
{
    networkFee = decimal.Parse(request.NetworkFee);
}
else if (!string.IsNullOrEmpty(request.FeeLevel))
{
    var multiplier = request.FeeLevel switch
    {
        "LOW" => 1.0m,
        "MEDIUM" => 1.5m,
        "HIGH" => 2.5m,
        _ => 1.5m
    };
    networkFee = asset.BaseFee * multiplier;
}
else
{
    networkFee = asset.BaseFee * 1.5m; // Default to medium
}

transaction.NetworkFee = networkFee;
transaction.TreatAsGrossAmount = request.TreatAsGrossAmount;
transaction.FeeCurrency = DetermineFeeCurrency(asset);
```

### Fee Currency Logic
```csharp
private string DetermineFeeCurrency(Asset asset)
{
    // For native assets, fee is in the same currency
    // For tokens, fee is in the parent chain's native currency
    return asset.ParentAssetId ?? asset.Id;
}
```

## Dependencies
- None (model fields already exist)

## Out of Scope
- Validating that fee is sufficient for network
- Dynamic fee adjustment based on network conditions
- Service fees (only network fees in this story)
