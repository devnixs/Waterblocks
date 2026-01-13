import type { Asset } from '../../types/admin';
import type { SetState } from './types';

type TransactionsFiltersProps = {
  assets: Asset[];
  filterAsset: string;
  setFilterAsset: SetState<string>;
  filterId: string;
  setFilterId: SetState<string>;
  filterHash: string;
  setFilterHash: SetState<string>;
  onClear: () => void;
};

export function TransactionsFilters({
  assets,
  filterAsset,
  setFilterAsset,
  filterId,
  setFilterId,
  filterHash,
  setFilterHash,
  onClear,
}: TransactionsFiltersProps) {
  return (
    <div className="flex gap-4 mb-6">
      <select
        value={filterAsset}
        onChange={(e) => setFilterAsset(e.target.value)}
        className="flex-1"
      >
        <option value="">All assets</option>
        {assets.map((asset) => (
          <option key={asset.id} value={asset.id}>
            {asset.name} ({asset.symbol})
          </option>
        ))}
      </select>
      <input
        type="text"
        placeholder="Filter by ID"
        value={filterId}
        onChange={(e) => setFilterId(e.target.value)}
        className="flex-1"
      />
      <input
        type="text"
        placeholder="Filter by hash"
        value={filterHash}
        onChange={(e) => setFilterHash(e.target.value)}
        className="flex-1"
      />
      <button
        className="btn btn-secondary"
        onClick={onClear}
        disabled={!filterAsset && !filterId && !filterHash}
      >
        Clear
      </button>
    </div>
  );
}
