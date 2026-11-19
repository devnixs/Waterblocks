import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useExchangeAccounts, useExchangeProviders } from '../api/queries';
import type { AdminExchangeAccount } from '../types/admin';
import { ConnectAccountDialog } from './exchanges/ConnectAccountDialog';
import { ExchangesTable } from './exchanges/ExchangesTable';
import { findFullProviders, oneAccountMessage } from './exchanges/exchangeFormat';

export default function ExchangesPage() {
  const navigate = useNavigate();
  const { data: accounts, isLoading, error } = useExchangeAccounts();
  const { data: providers } = useExchangeProviders();
  const [showConnectDialog, setShowConnectDialog] = useState(false);

  if (isLoading) return <div className="p-6 text-center text-muted">Loading exchange accounts...</div>;
  if (error) return <div className="p-6 text-center form-error">Error: {error.message}</div>;

  const openAccount = (account: AdminExchangeAccount) => navigate(`/exchanges/${account.id}`);
  const exchangeAccounts = accounts ?? [];
  const exchangeProviders = providers ?? [];
  const fullProviders = findFullProviders(exchangeProviders, exchangeAccounts);
  const allProvidersFull = fullProviders.length > 0 && fullProviders.length === exchangeProviders.length;

  return (
    <div>
      {exchangeAccounts.length === 0 ? (
        <div className="card text-center">
          <h2 className="mb-2">Connect an account</h2>
          <p className="text-muted mb-4">Connect an exchange simulator account to manage its collateral.</p>
          <button className="btn btn-primary" onClick={() => setShowConnectDialog(true)}>
            Connect account
          </button>
        </div>
      ) : (
        <>
          <div className="flex-between mb-4">
            <h2>Exchanges <span className="text-muted text-sm">({exchangeAccounts.length})</span></h2>
            {allProvidersFull ? (
              <span className="text-muted text-sm">{oneAccountMessage(fullProviders)}</span>
            ) : (
              <button className="btn btn-primary" onClick={() => setShowConnectDialog(true)}>
                Connect account
              </button>
            )}
          </div>
          <ExchangesTable accounts={exchangeAccounts} providers={exchangeProviders} onSelect={openAccount} />
        </>
      )}

      <ConnectAccountDialog
        open={showConnectDialog}
        onOpenChange={setShowConnectDialog}
        onConnected={openAccount}
        accounts={exchangeAccounts}
      />
    </div>
  );
}
