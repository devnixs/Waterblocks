import type { AdminExchangeAccount, AdminExchangeProvider } from '../../types/admin';
import { collateralSummary, exchangeName } from './exchangeFormat';

type ExchangesTableProps = {
  accounts: AdminExchangeAccount[];
  providers: AdminExchangeProvider[];
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

export function ExchangesTable({ accounts, providers, onSelect }: ExchangesTableProps) {
  return (
    <div className="overflow-x-auto rounded-lg border border-tertiary">
      <table className="w-full">
        <thead>
          <tr>
            <th>Name</th>
            <th>Exchange</th>
            <th>Simulator account</th>
            <th>Connection</th>
            <th>Collateral</th>
            <th>Created</th>
          </tr>
        </thead>
        <tbody>
          {accounts.map((account) => (
            <tr key={account.id} onClick={() => onSelect(account)} className="cursor-pointer">
              <td className="font-medium">{account.name}</td>
              <td>{exchangeName(providers, account.exchangeType)}</td>
              <td className="text-mono text-sm">{account.connection?.simulatorAccountName ?? '-'}</td>
              <td><ConnectionBadge account={account} /></td>
              <td className="text-mono">{collateralSummary(account)}</td>
              <td className="text-sm text-muted">{new Date(account.createdAt).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
