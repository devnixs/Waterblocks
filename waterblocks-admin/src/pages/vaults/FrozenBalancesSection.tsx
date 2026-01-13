type FrozenBalance = {
  assetId: string;
  amount: string;
};

type FrozenBalancesSectionProps = {
  balances?: FrozenBalance[];
  isLoading: boolean;
  error: unknown;
};

export function FrozenBalancesSection({ balances, isLoading, error }: FrozenBalancesSectionProps) {
  const errorMessage = error ? String(error) : '';
  return (
    <div className="mb-8">
      <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Frozen Balances</h3>
      {isLoading && <p className="text-muted">Loading frozen balances...</p>}
      {errorMessage && (
        <p className="text-danger">Error: {errorMessage}</p>
      )}
      {!isLoading && !error && (
        balances && balances.length > 0 ? (
          <div className="overflow-x-auto rounded-lg border border-tertiary">
            <table className="w-full text-sm">
              <thead>
                <tr>
                  <th>Asset</th>
                  <th>Amount</th>
                </tr>
              </thead>
              <tbody>
                {balances.map((balance) => (
                  <tr key={balance.assetId}>
                    <td className="font-bold">{balance.assetId}</td>
                    <td className="text-mono">{parseFloat(balance.amount).toFixed(8)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="p-4 text-center text-muted border border-dashed border-tertiary rounded-lg">
            No frozen balances
          </div>
        )
      )}
    </div>
  );
}
