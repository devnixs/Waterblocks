import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAssets, useExchangeAccount, useExchangeProviders, useExchangeTransfers } from '../api/queries';
import { CollateralTransferDialog } from './exchanges/CollateralTransferDialog';
import type { CollateralTransferTarget } from './exchanges/CollateralTransferDialog';
import { CollateralTransfersList } from './exchanges/CollateralTransfersList';
import { ConnectAccountDialog } from './exchanges/ConnectAccountDialog';
import { ExchangeAssetsTable } from './exchanges/ExchangeAssetsTable';
import { ExchangeConnectionCard } from './exchanges/ExchangeConnectionCard';
import { exchangeName, findProvider, listAccountAssets } from './exchanges/exchangeFormat';

const REFRESH_INTERVAL_MS = 3000;

export default function ExchangeAccountPage() {
  const { exchangeAccountId = '' } = useParams();
  const accountQuery = useExchangeAccount(exchangeAccountId, REFRESH_INTERVAL_MS);
  const transfersQuery = useExchangeTransfers(exchangeAccountId, REFRESH_INTERVAL_MS);
  const { data: providers } = useExchangeProviders();
  const { data: knownAssets } = useAssets();
  const [assetSearch, setAssetSearch] = useState('');
  const [showConnectDialog, setShowConnectDialog] = useState(false);
  const [transferTarget, setTransferTarget] = useState<CollateralTransferTarget | null>(null);
  const account = accountQuery.data;
  const exchangeProviders = useMemo(() => providers ?? [], [providers]);
  const provider = account ? findProvider(exchangeProviders, account.exchangeType) : undefined;

  const assets = useMemo(() => {
    if (!account) return [];
    // A transfer accepts the assets of the exchange simulator, or any known asset when the exchange has no simulator.
    const transferAssetIds = provider ? provider.assetIds : (knownAssets ?? []).map((asset) => asset.id);
    const search = assetSearch.trim().toLowerCase();
    return listAccountAssets(account, transferAssetIds)
      .filter((asset) => asset.assetId.toLowerCase().includes(search));
  }, [account, provider, knownAssets, assetSearch]);

  if (accountQuery.isLoading) return <div className="p-6 text-center text-muted">Loading exchange account...</div>;
  if (accountQuery.error || !account) {
    return (
      <div className="p-6 text-center form-error">
        Error: {accountQuery.error?.message ?? 'The exchange account was not found'}
      </div>
    );
  }

  const exchange = exchangeName(exchangeProviders, account.exchangeType);
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
          The {exchange} simulator cannot be read. Available amounts show 0 until it answers.
        </div>
      )}

      <div className="exchange-layout">
        <div>
          <ExchangeConnectionCard
            account={account}
            provider={provider}
            onConnect={() => setShowConnectDialog(true)}
          />
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
        <CollateralTransfersList transfers={transfersQuery.data ?? []} exchange={exchange} />
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
