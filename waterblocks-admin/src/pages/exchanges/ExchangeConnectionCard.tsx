import type { AdminExchangeAccount } from '../../types/admin';

type ExchangeConnectionCardProps = {
  account: AdminExchangeAccount;
  onConnect: () => void;
};

export function ExchangeConnectionCard({ account, onConnect }: ExchangeConnectionCardProps) {
  const { connection } = account;

  return (
    <div className="card">
      <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-3">Connection</h3>
      {connection ? (
        <div className="grid gap-2">
          <div className="exchange-detail-row">
            <span className="text-muted">Gate simulator account</span>
            <span className="text-mono">{connection.simulatorAccount}</span>
          </div>
          <div className="exchange-detail-row">
            <span className="text-muted">API key</span>
            <span className="text-mono">{connection.maskedApiKey}</span>
          </div>
        </div>
      ) : (
        <div className="flex-between">
          <span className="text-muted text-sm">This account has no Gate simulator connection.</span>
          <button className="btn btn-primary" onClick={onConnect}>Connect</button>
        </div>
      )}
    </div>
  );
}
