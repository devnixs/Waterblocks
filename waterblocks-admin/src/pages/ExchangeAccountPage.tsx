import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useExchangeAccount, useExchangeTransfers } from '../api/queries';
import type { AdminExchangeAccount, AdminExchangeAsset } from '../types/admin';
import { CollateralTransferDialog } from './exchanges/CollateralTransferDialog';
import type { CollateralTransferTarget } from './exchanges/CollateralTransferDialog';
import { CollateralTransfersList } from './exchanges/CollateralTransfersList';
import { ConnectAccountDialog } from './exchanges/ConnectAccountDialog';
import { ExchangeAssetsTable } from './exchanges/ExchangeAssetsTable';
import { ExchangeConnectionCard } from './exchanges/ExchangeConnectionCard';
import { createEmptyExchangeAsset, findExchangeAsset } from './exchanges/exchangeFormat';

const REFRESH_INTERVAL_MS = 3000;
const DEFAULT_ASSET_IDS = ['BTC', 'USDT'];

function listAccountAssets(account: AdminExchangeAccount): AdminExchangeAsset[] {
  const missingAssets = DEFAULT_ASSET_IDS
    .filter((assetId) => !findExchangeAsset(account, assetId))
    .map(createEmptyExchangeAsset);
  return [...account.assets, ...missingAssets].sort((left, right) => left.assetId.localeCompare(right.assetId));
}

export default function ExchangeAccountPage() {
  const { exchangeAccountId = '' } = useParams();
  const accountQuery = useExchangeAccount(exchangeAccountId, REFRESH_INTERVAL_MS);
  const transfersQuery = useExchangeTransfers(exchangeAccountId, REFRESH_INTERVAL_MS);
  const [assetSearch, setAssetSearch] = useState('');
  const [showConnectDialog, setShowConnectDialog] = useState(false);
  const [transferTarget, setTransferTarget] = useState<CollateralTransferTarget | null>(null);
  const account = accountQuery.data;

  const assets = useMemo(() => {
    if (!account) return [];
    const search = assetSearch.trim().toLowerCase();
    return listAccountAssets(account).filter((asset) => asset.assetId.toLowerCase().includes(search));
  }, [account, assetSearch]);

  if (accountQuery.isLoading) return <div className="p-6 text-center text-muted">Loading exchange account...</div>;
  if (accountQuery.error || !account) {
    return (
      <div className="p-6 text-center form-error">
        Error: {accountQuery.error?.message ?? 'The exchange account was not found'}
      </div>
    );
  }

  const refresh = () => {
    accountQuery.refetch();
    transfersQuery.refetch();
  };

  return (
    <div>
      <nav className="exchange-breadcrumb" aria-label="Breadcrumb">
        <Link to="/exchanges">Exchanges</Link>
        <span>&gt;</span>
        <span>{account.name} (Main account)</span>
      </nav>

      {!account.success && (
        <div className="exchange-warning" role="alert">
          The Gate simulator cannot be read. Available amounts show 0 until it answers.
        </div>
      )}

      <div className="exchange-layout">
        <div>
          <ExchangeConnectionCard account={account} onConnect={() => setShowConnectDialog(true)} />
          <div className="exchange-toolbar">
            <input
              type="text"
              placeholder="Search assets"
              aria-label="Search assets"
              value={assetSearch}
              onChange={(event) => setAssetSearch(event.target.value)}
            />
            <button className="btn btn-secondary" onClick={refresh}>Refresh</button>
          </div>
          <ExchangeAssetsTable
            assets={assets}
            onTransfer={(asset, direction) => setTransferTarget({ asset, direction })}
          />
        </div>
        <CollateralTransfersList transfers={transfersQuery.data ?? []} />
      </div>

      <ConnectAccountDialog
        open={showConnectDialog}
        onOpenChange={setShowConnectDialog}
        onConnected={() => undefined}
        account={account}
      />
      <CollateralTransferDialog
        account={account}
        target={transferTarget}
        onClose={() => setTransferTarget(null)}
      />
    </div>
  );
}
