import type { AdminExchangeAsset, CollateralDirection } from '../../types/admin';
import { formatExchangeAmount } from './exchangeFormat';

type ExchangeAssetsTableProps = {
  assets: AdminExchangeAsset[];
  onTransfer: (asset: AdminExchangeAsset, direction: CollateralDirection) => void;
};

export function ExchangeAssetsTable({ assets, onTransfer }: ExchangeAssetsTableProps) {
  return (
    <div className="overflow-x-auto rounded-lg border border-tertiary">
      <table className="w-full">
        <thead>
          <tr>
            <th>Asset</th>
            <th>Available</th>
            <th>Collateral</th>
            <th>Credit</th>
            <th>Locked</th>
            <th className="text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          {assets.map((asset) => (
            <tr key={asset.assetId} data-asset={asset.assetId}>
              <td className="font-medium">{asset.assetId}</td>
              <td className="text-mono">{formatExchangeAmount(asset.available)}</td>
              <td className="text-mono">{formatExchangeAmount(asset.total)}</td>
              <td className="text-mono">{formatExchangeAmount(asset.credit)}</td>
              <td className="text-mono">{formatExchangeAmount(asset.lockedAmount)}</td>
              <td>
                <div className="exchange-actions">
                  <button
                    className="btn btn-secondary text-sm py-1 px-3"
                    onClick={() => onTransfer(asset, 'ADD')}
                  >
                    Add collateral
                  </button>
                  <button
                    className="btn btn-secondary text-sm py-1 px-3"
                    onClick={() => onTransfer(asset, 'REMOVE')}
                    disabled={parseFloat(asset.available) === 0}
                  >
                    Remove collateral
                  </button>
                </div>
              </td>
            </tr>
          ))}
          {assets.length === 0 && (
            <tr>
              <td colSpan={6} className="text-center text-muted">No assets found</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
