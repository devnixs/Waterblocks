import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useExchangeAccounts } from '../api/queries';
import type { AdminExchangeAccount } from '../types/admin';
import { ConnectAccountDialog } from './exchanges/ConnectAccountDialog';
import { ExchangesTable } from './exchanges/ExchangesTable';

export default function ExchangesPage() {
  const navigate = useNavigate();
  const { data: accounts, isLoading, error } = useExchangeAccounts();
  const [showConnectDialog, setShowConnectDialog] = useState(false);

  if (isLoading) return <div className="p-6 text-center text-muted">Loading exchange accounts...</div>;
  if (error) return <div className="p-6 text-center form-error">Error: {error.message}</div>;

  const openAccount = (account: AdminExchangeAccount) => navigate(`/exchanges/${account.id}`);
  const exchangeAccounts = accounts ?? [];
  const hasGateAccount = exchangeAccounts.some((account) => account.exchangeType === 'GATEIO');

  return (
    <div>
      {exchangeAccounts.length === 0 ? (
        <div className="card text-center">
          <h2 className="mb-2">Connect an account</h2>
          <p className="text-muted mb-4">Connect a Gate simulator account to manage its collateral.</p>
          <button className="btn btn-primary" onClick={() => setShowConnectDialog(true)}>
            Connect account
          </button>
        </div>
      ) : (
        <>
          <div className="flex-between mb-4">
            <h2>Exchanges <span className="text-muted text-sm">({exchangeAccounts.length})</span></h2>
            {hasGateAccount ? (
              <span className="text-muted text-sm">A workspace can have only one Gate account.</span>
            ) : (
              <button className="btn btn-primary" onClick={() => setShowConnectDialog(true)}>
                Connect account
              </button>
            )}
          </div>
          <ExchangesTable accounts={exchangeAccounts} onSelect={openAccount} />
        </>
      )}

      <ConnectAccountDialog
        open={showConnectDialog}
        onOpenChange={setShowConnectDialog}
        onConnected={openAccount}
      />
    </div>
  );
}
