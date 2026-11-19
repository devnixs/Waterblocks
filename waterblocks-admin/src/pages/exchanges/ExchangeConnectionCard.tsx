import type { AdminExchangeAccount, AdminExchangeProvider } from '../../types/admin';

type ExchangeConnectionCardProps = {
  account: AdminExchangeAccount;
  provider?: AdminExchangeProvider;
  onConnect: () => void;
};

export function ExchangeConnectionCard({ account, provider, onConnect }: ExchangeConnectionCardProps) {
  const { connection } = account;

  return (
    <div className="card">
      <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-3">Connection</h3>
      {connection && (
        <div className="grid gap-2">
          <div className="exchange-detail-row">
            <span className="text-muted">Simulator account</span>
            <span className="text-mono">{connection.simulatorAccountName}</span>
          </div>
          <div className="exchange-detail-row">
            <span className="text-muted">API key</span>
            <span className="text-mono">{connection.maskedApiKey}</span>
          </div>
        </div>
      )}
      {!connection && provider && (
        <div className="flex-between">
          <span className="text-muted text-sm">This account has no simulator connection.</span>
          <button className="btn btn-primary" onClick={onConnect}>Connect</button>
        </div>
      )}
      {!connection && !provider && (
        <span className="text-muted text-sm">Waterblocks has no simulator for this exchange.</span>
      )}
    </div>
  );
}
