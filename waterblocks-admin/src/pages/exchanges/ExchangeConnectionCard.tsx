import type { AdminExchange, AdminExchangeAccount } from '../../types/admin';

type ExchangeConnectionCardProps = {
  account: AdminExchangeAccount;
  exchange?: AdminExchange;
  onConnect: () => void;
};

export function ExchangeConnectionCard({ account, exchange, onConnect }: ExchangeConnectionCardProps) {
  const { connection } = account;
  const canConnect = !!exchange?.isConnectable;

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
      {!connection && canConnect && (
        <div className="flex-between">
          <span className="text-muted text-sm">This account has no simulator connection.</span>
          <button className="btn btn-primary" onClick={onConnect}>Connect</button>
        </div>
      )}
      {!connection && !canConnect && (
        <span className="text-muted text-sm">
          This exchange has no simulator URL. Set one on the Supported exchanges page to connect this account.
        </span>
      )}
    </div>
  );
}
