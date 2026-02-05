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
  const formatAmount = (value: number, currency: string) => {
    if (!Number.isFinite(value)) return '-';
    const display = value === 0 ? '0' : value.toFixed(8).replace(/\.?0+$/, '');
    return `${display} ${currency}`;
  };

  const amountValue = parseFloat(transaction.amount);
  const feeValue = parseFloat(transaction.networkFee);
  const feeCurrency = transaction.feeCurrency || transaction.assetId;
  const isFeeCurrencyDifferent = !!transaction.feeCurrency && transaction.feeCurrency !== transaction.assetId;
  const senderSendsValue = transaction.treatAsGrossAmount ? amountValue : amountValue + (Number.isFinite(feeValue) ? feeValue : 0);
  const recipientReceivesValue = transaction.treatAsGrossAmount
    ? Math.max(0, amountValue - (Number.isFinite(feeValue) ? feeValue : 0))
    : amountValue;
  const feeDisplay = Number.isFinite(feeValue)
    ? (feeValue > 0 ? formatAmount(feeValue, feeCurrency) : '0')
    : '-';
  return (
    <div className="detail-panel">
      <div className="detail-panel-header">
        <h2>Transaction Details</h2>
        <button className="close-btn" onClick={onClose}>x</button>
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
        <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Fees</h3>
        <div className="grid gap-3 p-4 bg-tertiary/20 rounded-lg border border-tertiary">
          <div className="flex justify-between">
            <span className="text-muted">Network Fee</span>
            <span className="text-mono">
              {feeDisplay}
            </span>
          </div>
          {transaction.feeCurrency && transaction.feeCurrency !== transaction.assetId && (
            <div className="flex justify-between">
              <span className="text-muted">Fee Currency</span>
              <span>{transaction.feeCurrency}</span>
            </div>
          )}
          <div className="flex justify-between">
            <span className="text-muted">Sender Sends</span>
            <span className="text-mono">
              {isFeeCurrencyDifferent
                ? `${formatAmount(amountValue, transaction.assetId)} + ${feeDisplay}`
                : formatAmount(senderSendsValue, transaction.assetId)}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted">Recipient Receives</span>
            <span className="text-mono">
              {isFeeCurrencyDifferent
                ? formatAmount(amountValue, transaction.assetId)
                : formatAmount(recipientReceivesValue, transaction.assetId)}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted">Fee Handling</span>
            <span className={transaction.treatAsGrossAmount ? 'text-yellow-400' : 'text-muted'}>
              {transaction.treatAsGrossAmount ? 'Deducted from amount' : 'Added to amount'}
            </span>
          </div>
        </div>
      </div>

      <div className="mb-8">
        <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Flow</h3>
        <div className="grid gap-4">
          <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
            <div className="text-xs text-muted uppercase mb-1">Source</div>
            <div className="font-medium">{transaction.sourceType}</div>
            {transaction.sourceType === 'INTERNAL' && (
              <div className="text-sm mt-1">
                Vault: {transaction.sourceVaultAccountName || transaction.vaultAccountId}
              </div>
            )}
            {transaction.sourceAddress && (
              <div className="text-mono text-xs text-muted break-all mt-1">
                {transaction.sourceAddress}
              </div>
            )}
            {!transaction.sourceAddress && transaction.sourceType === 'EXTERNAL' && (
              <div className="text-muted text-sm mt-1">-</div>
            )}
          </div>

          <div className="flex justify-center text-muted">-&gt;</div>

          <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
            <div className="text-xs text-muted uppercase mb-1">Destination</div>
            <div className="font-medium">{transaction.destinationType}</div>
            {transaction.destinationType === 'INTERNAL' && (
              <div className="text-sm mt-1">
                Vault: {transaction.destinationVaultAccountName || transaction.vaultAccountId}
              </div>
            )}
            {transaction.destinationAddress && (
              <div className="text-mono text-xs text-muted break-all mt-1">
                {transaction.destinationAddress}
              </div>
            )}
            {transaction.destinationTag ? (
              <div className="text-mono text-xs text-muted break-all mt-1">
                Tag: {transaction.destinationTag}
              </div>
            ) : (
              <div className="text-muted text-xs mt-1">Tag: -</div>
            )}
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
