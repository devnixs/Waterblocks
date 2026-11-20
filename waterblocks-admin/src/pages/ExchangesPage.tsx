import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useExchangeAccounts, useExchanges } from '../api/queries';
import type { AdminExchangeAccount } from '../types/admin';
import { ConnectAccountDialog } from './exchanges/ConnectAccountDialog';
import { ExchangesTable } from './exchanges/ExchangesTable';
import { findFullExchanges, oneAccountMessage } from './exchanges/exchangeFormat';

export default function ExchangesPage() {
  const navigate = useNavigate();
  const { data: accounts, isLoading, error } = useExchangeAccounts();
  const { data: exchanges } = useExchanges();
  const [showConnectDialog, setShowConnectDialog] = useState(false);

  if (isLoading) return <div className="p-6 text-center text-muted">Loading exchange accounts...</div>;
  if (error) return <div className="p-6 text-center form-error">Error: {error.message}</div>;

  const openAccount = (account: AdminExchangeAccount) => navigate(`/exchanges/${account.id}`);
  const exchangeAccounts = accounts ?? [];
  const catalog = exchanges ?? [];
  const connectable = catalog.filter((exchange) => exchange.isConnectable);
  const fullExchanges = findFullExchanges(connectable, exchangeAccounts);
  const allExchangesFull = fullExchanges.length > 0 && fullExchanges.length === connectable.length;
  const catalogLink = <Link to="/exchanges/supported" className="btn btn-secondary">Supported exchanges</Link>;

  return (
    <div>
      {exchangeAccounts.length === 0 ? (
        <div className="card text-center">
          <h2 className="mb-2">Connect an account</h2>
          <p className="text-muted mb-4">Connect an exchange simulator account to manage its collateral.</p>
          <div className="flex gap-2 justify-center">
            <button className="btn btn-primary" onClick={() => setShowConnectDialog(true)}>
              Connect account
            </button>
            {catalogLink}
          </div>
        </div>
      ) : (
        <>
          <div className="flex-between mb-4">
            <h2>Exchanges <span className="text-muted text-sm">({exchangeAccounts.length})</span></h2>
            <div className="flex gap-2 items-center">
              {allExchangesFull ? (
                <span className="text-muted text-sm">{oneAccountMessage(fullExchanges)}</span>
              ) : (
                <button className="btn btn-primary" onClick={() => setShowConnectDialog(true)}>
                  Connect account
                </button>
              )}
              {catalogLink}
            </div>
          </div>
          <ExchangesTable accounts={exchangeAccounts} exchanges={catalog} onSelect={openAccount} />
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
