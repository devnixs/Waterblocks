import type { AdminTransaction } from '../../types/admin';

type TransactionsTableProps = {
  transactions: AdminTransaction[];
  selectedIds: Set<string>;
  selectedIndex: number;
  onSelect: (transaction: AdminTransaction) => void;
  onToggleSelection: (id: string) => void;
  onToggleAll: (checked: boolean) => void;
  formatVaultLabel: (id?: string, name?: string) => string;
};

export function TransactionsTable({
  transactions,
  selectedIds,
  selectedIndex,
  onSelect,
  onToggleSelection,
  onToggleAll,
  formatVaultLabel,
}: TransactionsTableProps) {
  return (
    <div className="overflow-x-auto rounded-lg border border-tertiary">
      <table className="w-full">
        <thead>
          <tr>
            <th className="w-10 text-center">
              <input
                type="checkbox"
                checked={selectedIds.size === transactions.length && transactions.length > 0}
                onChange={(e) => onToggleAll(e.target.checked)}
                className="cursor-pointer"
              />
            </th>
            <th>ID</th>
            <th>State</th>
            <th>Asset</th>
            <th>Amount</th>
            <th>Source</th>
            <th>Destination</th>
            <th>Created</th>
            <th className="text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          {transactions.map((tx, index) => (
            <tr
              key={tx.id}
              onClick={() => onSelect(tx)}
              className={`cursor-pointer transition-colors ${selectedIndex === index ? 'bg-white/5' : 'hover:bg-white/5'}`}
              style={selectedIndex === index ? { backgroundColor: 'var(--bg-tertiary)' } : undefined}
            >
              <td className="text-center" onClick={(e) => e.stopPropagation()}>
                <input
                  type="checkbox"
                  checked={selectedIds.has(tx.id)}
                  onChange={() => onToggleSelection(tx.id)}
                  className="cursor-pointer"
                />
              </td>
              <td className="text-mono text-sm text-muted">
                {tx.id.substring(0, 8)}...
              </td>
              <td>
                <span className={`state-badge state-${tx.state}`}>
                  {tx.state}
                </span>
              </td>
              <td className="font-medium">{tx.assetId}</td>
              <td className="text-mono">{parseFloat(tx.amount).toFixed(4)}</td>
              <td className="text-mono text-sm text-muted">
                {formatVaultLabel(tx.vaultAccountId)}
              </td>
              <td className="text-mono text-sm text-muted">
                {tx.destinationType === 'INTERNAL'
                  ? formatVaultLabel(tx.destinationVaultAccountId, tx.destinationVaultAccountName)
                  : `${tx.destinationAddress.substring(0, 12)}...`}
              </td>
              <td className="text-sm text-muted">{new Date(tx.createdAt).toLocaleString()}</td>
              <td className="text-right">
                <button
                  className="btn btn-ghost text-sm py-1 px-3"
                  onClick={(e) => {
                    e.stopPropagation();
                    onSelect(tx);
                  }}
                >
                  View
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
