# Story 04: Fee Deduction Mode Toggle

## Summary
Add a toggle that allows users to specify whether transaction fees should be deducted from the specified amount (gross) or added to it (net).

## Background
When sending cryptocurrency, there are two ways to handle fees:
1. **Net Amount (fees added)**: Send exactly the specified amount, pay fees on top
   - User enters "1 BTC", recipient receives 1 BTC, sender pays 1 BTC + fees
2. **Gross Amount (fees deducted)**: Deduct fees from the specified amount
   - User enters "1 BTC", recipient receives 1 BTC - fees, sender pays exactly 1 BTC

The Transaction model already has a `TreatAsGrossAmount` boolean field to support this.

## Acceptance Criteria

### AC1: Fee mode toggle is displayed
- Add a toggle/switch below the fee tier selector
- Label: "Deduct fees from amount" or similar
- Default: OFF (fees are added to amount = net amount mode)

### AC2: Visual feedback on amount impact
- When toggle is ON: Show calculated recipient amount (amount - estimated fee)
- When toggle is OFF: Show that recipient receives full amount
- Example display:
  ```
  Amount: 1.0 BTC
  Fee: 0.0001 BTC (Medium)

  □ Deduct fees from amount
  → Recipient receives: 1.0 BTC
  → Total cost: 1.0001 BTC

  ☑ Deduct fees from amount
  → Recipient receives: 0.9999 BTC
  → Total cost: 1.0 BTC
  ```

### AC3: Form includes fee deduction flag
- Include `treatAsGrossAmount` in the transaction creation request
- Value: `true` when toggle is ON, `false` when OFF

### AC4: Backend accepts and stores the flag
- Update `CreateAdminTransactionRequestDto` to include `TreatAsGrossAmount`
- Store the value in the Transaction entity

## UI Design

### Toggle with Explanation
```
Fee Handling:
  ┌─────────────────────────────────────────────┐
  │ □ Deduct fees from amount                   │
  │   When enabled, fees are subtracted from    │
  │   the amount you enter. The recipient       │
  │   receives less than the specified amount.  │
  └─────────────────────────────────────────────┘
```

### Summary Display
```
Transaction Summary:
  Amount entered:     1.00000000 BTC
  Network fee:       -0.00001500 BTC
  ────────────────────────────────
  Recipient receives: 0.99998500 BTC
```

## Technical Notes

### Existing Model Field
```csharp
// In Transaction.cs - already exists
public bool TreatAsGrossAmount { get; set; } = false;
```

### DTO Updates Required
```csharp
// Add to CreateAdminTransactionRequestDto
public bool TreatAsGrossAmount { get; set; } = false;
```

### Frontend State
```typescript
const [treatAsGrossAmount, setTreatAsGrossAmount] = useState(false);

// Calculate display values
const recipientAmount = treatAsGrossAmount
  ? parseFloat(amount) - parseFloat(selectedFee)
  : parseFloat(amount);

const totalCost = treatAsGrossAmount
  ? parseFloat(amount)
  : parseFloat(amount) + parseFloat(selectedFee);
```

### Fireblocks Terminology
In the Fireblocks API, this is controlled by the `treatAsGrossAmount` field in `TransactionRequest`:
- `true`: The specified amount includes the fee (gross)
- `false`: The specified amount is net, fee is added on top

## Dependencies
- Story 02: Fee Selection UI Component (to have fee values available)

## Out of Scope
- Different fee currencies (e.g., ERC-20 tokens paying gas in ETH)
- Calculating exact recipient amount server-side
