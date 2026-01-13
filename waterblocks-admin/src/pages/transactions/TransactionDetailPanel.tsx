import type { AdminTransaction } from '../../types/admin';

type TransactionDetailPanelProps = {
  transaction: AdminTransaction;
  onClose: () => void;
  getAvailableActions: (state: string) => string[];
  onTransition: (id: string, action: string, reason?: string) => void;
};

export function TransactionDetailPanel({
  transaction,
  onClose,
  getAvailableActions,
  onTransition,
}: TransactionDetailPanelProps) {
  return (
    <div className="detail-panel">
      <div className="detail-panel-header">
        <h2>Transaction Details</h2>
        <button className="close-btn" onClick={onClose}>×</button>
      </div>

      <div className="mb-8">
        <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Information</h3>
        <div className="grid gap-3 p-4 bg-tertiary/20 rounded-lg border border-tertiary">
          <div className="flex justify-between">
            <span className="text-muted">ID</span>
            <span className="text-mono select-all">{transaction.id}</span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-muted">State</span>
            <span className={`state-badge state-${transaction.state}`}>{transaction.state}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted">Asset</span>
            <span className="font-medium">{transaction.assetId}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted">Amount</span>
            <span className="text-mono">{transaction.amount}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted">Created</span>
            <span>{new Date(transaction.createdAt).toLocaleString()}</span>
          </div>
        </div>
      </div>

      <div className="mb-8">
        <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Flow</h3>
        <div className="grid gap-4">
          <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
            <div className="text-xs text-muted uppercase mb-1">Source</div>
            <div className="font-medium">{transaction.sourceType}</div>
            <div className="text-mono text-sm text-muted break-all mt-1">
              {transaction.sourceType === 'INTERNAL'
                ? `Vault: ${transaction.vaultAccountId}`
                : transaction.sourceAddress}
            </div>
          </div>

          <div className="flex justify-center text-muted">↓</div>

          <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
            <div className="text-xs text-muted uppercase mb-1">Destination</div>
            <div className="font-medium">{transaction.destinationType}</div>
            <div className="text-mono text-sm text-muted break-all mt-1">
              {transaction.destinationType === 'INTERNAL'
                ? `Vault: ${transaction.destinationVaultAccountId}`
                : transaction.destinationAddress}
            </div>
          </div>
        </div>
      </div>

      {transaction.hash && (
        <div className="mb-8">
          <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Blockchain</h3>
          <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
            <div className="text-xs text-muted uppercase mb-1">Transaction Hash</div>
            <div className="text-mono text-sm break-all text-accent cursor-pointer hover:underline">
              {transaction.hash}
            </div>
          </div>
        </div>
      )}

      <div>
        <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Actions</h3>
        <div className="flex flex-wrap gap-2">
          {getAvailableActions(transaction.state).map((action) => (
            <button
              key={action}
              className={`btn ${action === 'fail' || action === 'reject' || action === 'cancel' || action === 'timeout'
                ? 'btn-danger'
                : 'btn-primary'
                }`}
              onClick={() => {
                if (action === 'fail') {
                  const reason = prompt('Enter failure reason:');
                  if (reason) onTransition(transaction.id, 'fail', reason);
                } else if (action === 'cancel') {
                  if (confirm('Are you sure?')) onTransition(transaction.id, 'cancel');
                } else {
                  onTransition(transaction.id, action);
                }
              }}
            >
              {action.charAt(0).toUpperCase() + action.slice(1)}
            </button>
          ))}
          {getAvailableActions(transaction.state).length === 0 && (
            <div className="text-muted text-sm italic">No actions available for this state</div>
          )}
        </div>
      </div>
    </div>
  );
}
