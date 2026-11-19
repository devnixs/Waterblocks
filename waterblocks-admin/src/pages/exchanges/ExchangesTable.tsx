import type { AdminExchangeAccount } from '../../types/admin';
import { findExchangeAsset, formatExchangeAmount } from './exchangeFormat';

type ExchangesTableProps = {
  accounts: AdminExchangeAccount[];
  onSelect: (account: AdminExchangeAccount) => void;
};

function ConnectionBadge({ account }: { account: AdminExchangeAccount }) {
  if (!account.connection) {
    return <span className="state-badge state-SUBMITTED">Not connected</span>;
  }

  return account.success
    ? <span className="state-badge state-COMPLETED">Connected</span>
    : <span className="state-badge state-FAILED">Unavailable</span>;
}

export function ExchangesTable({ accounts, onSelect }: ExchangesTableProps) {
  return (
    <div className="overflow-x-auto rounded-lg border border-tertiary">
      <table className="w-full">
        <thead>
          <tr>
            <th>Name</th>
            <th>Exchange</th>
            <th>Gate simulator account</th>
            <th>Connection</th>
            <th>Collateral BTC</th>
            <th>Available BTC</th>
            <th>Credit BTC</th>
            <th>Created</th>
          </tr>
        </thead>
        <tbody>
          {accounts.map((account) => {
            const btc = findExchangeAsset(account, 'BTC');
            return (
              <tr key={account.id} onClick={() => onSelect(account)} className="cursor-pointer">
                <td className="font-medium">{account.name}</td>
                <td>{account.exchangeType}</td>
                <td className="text-mono text-sm">{account.connection?.simulatorAccount ?? '-'}</td>
                <td><ConnectionBadge account={account} /></td>
                <td className="text-mono">{formatExchangeAmount(btc?.total)}</td>
                <td className="text-mono">{formatExchangeAmount(btc?.available)}</td>
                <td className="text-mono">{formatExchangeAmount(btc?.credit)}</td>
                <td className="text-sm text-muted">{new Date(account.createdAt).toLocaleString()}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
