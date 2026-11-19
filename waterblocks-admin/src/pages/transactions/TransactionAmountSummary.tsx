type TransactionAmountSummaryProps = {
  amount: string;
  networkFee: string;
  symbol: string;
  treatAsGrossAmount: boolean;
};

export function formatFee(fee: string | undefined): string {
  if (!fee) return '0';
  const num = parseFloat(fee);
  if (isNaN(num)) return '0';
  if (num === 0) return '0';
  if (num < 0.000001) return num.toExponential(2);
  return num.toFixed(8).replace(/\.?0+$/, '');
}

export function TransactionAmountSummary({
  amount,
  networkFee,
  symbol,
  treatAsGrossAmount,
}: TransactionAmountSummaryProps) {
  const parsedAmount = parseFloat(amount) || 0;
  const parsedFee = parseFloat(networkFee) || 0;

  if (parsedAmount <= 0) {
    return null;
  }

  const recipientReceives = treatAsGrossAmount
    ? Math.max(0, parsedAmount - parsedFee)
    : parsedAmount;
  const totalCost = treatAsGrossAmount
    ? parsedAmount
    : parsedAmount + parsedFee;

  return (
    <div className="bg-gray-800 rounded p-3 text-sm">
      <div className="flex justify-between mb-1">
        <span className="text-muted">Amount entered:</span>
        <span>{parsedAmount} {symbol}</span>
      </div>
      <div className="flex justify-between mb-1">
        <span className="text-muted">Network fee:</span>
        <span>{treatAsGrossAmount ? '-' : '+'}{formatFee(networkFee)} {symbol}</span>
      </div>
      <div className="border-t border-gray-700 my-2" />
      <div className="flex justify-between">
        <span className="text-muted">Recipient receives:</span>
        <span className="font-medium">{formatFee(recipientReceives.toString())} {symbol}</span>
      </div>
      <div className="flex justify-between">
        <span className="text-muted">Total cost:</span>
        <span className="font-medium">{formatFee(totalCost.toString())} {symbol}</span>
      </div>
    </div>
  );
}
