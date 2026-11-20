import { Link } from 'react-router-dom';
import type { AdminCollateralTransfer, ExchangeMirrorState } from '../../types/admin';
import { trimAmount } from './exchangeFormat';

function mirrorLabel(state: ExchangeMirrorState | undefined, exchange: string): string | undefined {
  switch (state) {
    case 'PENDING':
      return `Waiting for ${exchange}`;
    case 'APPLIED':
      return `Applied at ${exchange}`;
    case 'REJECTED':
      return `Rejected by ${exchange}`;
    case 'FAILED':
      return `${exchange} update failed`;
    default:
      return undefined;
  }
}

type TransferItemProps = {
  transfer: AdminCollateralTransfer;
  exchange: string;
};

function TransferItem({ transfer, exchange }: TransferItemProps) {
  const label = mirrorLabel(transfer.mirrorState, exchange);

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
      {label && <div>{label}</div>}
      {transfer.mirrorError && <div className="form-error">{transfer.mirrorError}</div>}
      <div className="text-xs text-muted">
        {transfer.initiatedBy ? `${transfer.initiatedBy}, ` : ''}
        {new Date(transfer.createdAt).toLocaleString()}
      </div>
      <Link to={`/transactions/${encodeURIComponent(transfer.id)}`}>View transaction</Link>
    </div>
  );
}

type CollateralTransfersListProps = {
  transfers: AdminCollateralTransfer[];
  exchange: string;
};

export function CollateralTransfersList({ transfers, exchange }: CollateralTransfersListProps) {
  return (
    <div className="card">
      <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-3">
        Collateral transfers <span>({transfers.length})</span>
      </h3>
      {transfers.length === 0 ? (
        <div className="text-sm text-muted">No collateral transfers yet</div>
      ) : (
        transfers.map((transfer) => <TransferItem key={transfer.id} transfer={transfer} exchange={exchange} />)
      )}
    </div>
  );
}
