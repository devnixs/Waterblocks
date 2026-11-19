import { Link } from 'react-router-dom';
import type { AdminCollateralTransfer, ExchangeMirrorState } from '../../types/admin';
import { trimAmount } from './exchangeFormat';

const MIRROR_LABELS: Partial<Record<ExchangeMirrorState, string>> = {
  PENDING: 'Waiting for Gate',
  APPLIED: 'Applied at Gate',
  REJECTED: 'Rejected by Gate',
  FAILED: 'Gate update failed',
};

function TransferItem({ transfer }: { transfer: AdminCollateralTransfer }) {
  const mirrorLabel = transfer.mirrorState ? MIRROR_LABELS[transfer.mirrorState] : undefined;

  return (
    <div className="exchange-transfer" data-transfer-id={transfer.id}>
      <div className="flex-between gap-2">
        <span className="text-xs uppercase tracking-wider text-muted font-bold">
          {transfer.direction === 'ADD' ? 'ADD COLLATERAL' : 'REMOVE COLLATERAL'}
        </span>
        <span className={`state-badge state-${transfer.state}`}>{transfer.state}</span>
      </div>
      <div>{transfer.fromName} to {transfer.toName}</div>
      <div className="text-mono">{trimAmount(transfer.amount)} {transfer.assetId}</div>
      {transfer.note && <div className="text-muted">{transfer.note}</div>}
      {mirrorLabel && <div>{mirrorLabel}</div>}
      {transfer.mirrorError && <div className="form-error">{transfer.mirrorError}</div>}
      <div className="text-xs text-muted">
        {transfer.initiatedBy ? `${transfer.initiatedBy}, ` : ''}
        {new Date(transfer.createdAt).toLocaleString()}
      </div>
      <Link to={`/transactions/${encodeURIComponent(transfer.id)}`}>View transaction</Link>
    </div>
  );
}

export function CollateralTransfersList({ transfers }: { transfers: AdminCollateralTransfer[] }) {
  return (
    <div className="card">
      <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-3">
        Collateral transfers <span>({transfers.length})</span>
      </h3>
      {transfers.length === 0 ? (
        <div className="text-sm text-muted">No collateral transfers yet</div>
      ) : (
        transfers.map((transfer) => <TransferItem key={transfer.id} transfer={transfer} />)
      )}
    </div>
  );
}
