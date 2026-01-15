import { Link } from 'react-router-dom';
import type { AdminTransaction } from '../../types/admin';

type TransactionsTableProps = {
  transactions: AdminTransaction[];
  selectedIds: Set<string>;
  selectedIndex: number;
  onSelect: (transaction: AdminTransaction) => void;
  onToggleSelection: (id: string) => void;
  onToggleAll: (checked: boolean) => void;
};

export function TransactionsTable({
  transactions,
  selectedIds,
  selectedIndex,
  onSelect,
  onToggleSelection,
  onToggleAll,
}: TransactionsTableProps) {
  const formatInternalLabel = (name?: string, fallbackId?: string) => {
    if (name) return name;
    if (fallbackId) return `${fallbackId.substring(0, 8)}...`;
    return '-';
  };

  const formatAddress = (address?: string) => {
    if (!address) return '-';
    if (address.length <= 16) return address;
    return `${address.substring(0, 12)}...${address.substring(address.length - 4)}`;
  };

  const buildVaultLink = (label: string, vaultName?: string, vaultId?: string) => {
    if (!vaultName && !vaultId) return <span>{label}</span>;
    const query = vaultId ? `vaultId=${encodeURIComponent(vaultId)}` : `vaultName=${encodeURIComponent(vaultName ?? '')}`;
    return (
      <Link
        to={`/vaults?${query}`}
        onClick={(event) => event.stopPropagation()}
        className="text-primary hover:underline"
      >
        {label}
      </Link>
    );
  };

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
                {tx.sourceType === 'INTERNAL' ? (
                  <div className="flex flex-col gap-1">
                    <span>
                      {buildVaultLink(
                        formatInternalLabel(tx.sourceVaultAccountName, tx.vaultAccountId),
                        tx.sourceVaultAccountName,
                        tx.sourceVaultAccountName ? undefined : tx.vaultAccountId,
                      )}
                    </span>
                    <span className="text-xs text-muted" title={tx.sourceAddress}>
                      {formatAddress(tx.sourceAddress)}
                    </span>
                  </div>
                ) : (
                  <span title={tx.sourceAddress}>{formatAddress(tx.sourceAddress)}</span>
                )}
              </td>
              <td className="text-mono text-sm text-muted">
                {tx.destinationType === 'INTERNAL' ? (
                  <div className="flex flex-col gap-1">
                    <span>
                      {buildVaultLink(
                        formatInternalLabel(tx.destinationVaultAccountName),
                        tx.destinationVaultAccountName,
                      )}
                    </span>
                    <span className="text-xs text-muted" title={tx.destinationAddress}>
                      {formatAddress(tx.destinationAddress)}
                    </span>
                  </div>
                ) : (
                  <span title={tx.destinationAddress}>{formatAddress(tx.destinationAddress)}</span>
                )}
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
