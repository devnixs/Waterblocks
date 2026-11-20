import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useExchanges } from '../api/queries';
import type { AdminExchange } from '../types/admin';
import { ExchangeEditDialog } from './exchanges/ExchangeEditDialog';

type EditTarget = { exchange?: AdminExchange };

export default function SupportedExchangesPage() {
  const { data: exchanges, isLoading, error } = useExchanges();
  const [editTarget, setEditTarget] = useState<EditTarget | null>(null);

  if (isLoading) return <div className="p-6 text-center text-muted">Loading exchanges...</div>;
  if (error) return <div className="p-6 text-center form-error">Error: {error.message}</div>;

  const catalog = exchanges ?? [];

  return (
    <div>
      <nav className="exchange-breadcrumb" aria-label="Breadcrumb">
        <Link to="/exchanges">Exchanges</Link>
        <span>&gt;</span>
        <span>Supported exchanges</span>
      </nav>
      <div className="flex-between mb-4">
        <h2>Supported exchanges <span className="text-muted text-sm">({catalog.length})</span></h2>
        <button className="btn btn-primary" onClick={() => setEditTarget({})}>Add exchange</button>
      </div>
      <div className="overflow-x-auto rounded-lg border border-tertiary">
        <table className="w-full">
          <thead>
            <tr>
              <th>Exchange type</th>
              <th>Name</th>
              <th>Simulator URL</th>
              <th>One account per workspace</th>
              <th>Currencies</th>
              <th className="text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {catalog.map((exchange) => (
              <tr key={exchange.exchangeType} data-exchange={exchange.exchangeType}>
                <td className="text-mono">{exchange.exchangeType}</td>
                <td className="font-medium">{exchange.name}</td>
                <td className="text-mono text-sm">{exchange.simulatorBaseUrl ?? '-'}</td>
                <td>{exchange.oneAccountPerWorkspace ? 'Yes' : 'No'}</td>
                <td className="text-mono text-sm">
                  {exchange.currencies.length > 0
                    ? exchange.currencies.map((currency) => `${currency.assetId} = ${currency.currency}`).join(', ')
                    : '-'}
                </td>
                <td className="text-right">
                  <button className="btn btn-secondary text-sm py-1 px-3" onClick={() => setEditTarget({ exchange })}>
                    Edit
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ExchangeEditDialog
        open={editTarget !== null}
        exchange={editTarget?.exchange}
        onClose={() => setEditTarget(null)}
      />
    </div>
  );
}
