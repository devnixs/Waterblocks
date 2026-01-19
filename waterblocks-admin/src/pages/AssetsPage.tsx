import { useState } from 'react';
import { useAdminAssets, useCreateAdminAsset, useDeleteAdminAsset, useUpdateAdminAsset } from '../api/queries';
import type { AdminAsset, BlockchainType } from '../types/admin';
import { useToast } from '../components/ToastProvider';
import { AssetEditPanel } from './assets/AssetEditPanel';

const blockchainOptions: BlockchainType[] = ['AccountBased', 'AddressBased', 'MemoBased'];

const emptyDraft = {
  assetId: '',
  name: '',
  symbol: '',
  decimals: '18',
  type: '',
  blockchainType: 'AccountBased' as BlockchainType,
  contractAddress: '',
  nativeAsset: '',
  baseFee: '',
  feeAssetId: '',
  isActive: true,
};

export default function AssetsPage() {
  const { data: assets, isLoading, error } = useAdminAssets();
  const createAsset = useCreateAdminAsset();
  const updateAsset = useUpdateAdminAsset();
  const deleteAsset = useDeleteAdminAsset();
  const { showToast } = useToast();
  const [draft, setDraft] = useState({ ...emptyDraft });
  const [selectedAsset, setSelectedAsset] = useState<AdminAsset | null>(null);

  const handleCreate = async () => {
    const assetId = draft.assetId.trim().toUpperCase();
    const name = draft.name.trim();
    const symbol = draft.symbol.trim().toUpperCase();

    if (!assetId) {
      showToast({ title: 'Asset ID is required', type: 'error' });
      return;
    }

    if (!name) {
      showToast({ title: 'Asset name is required', type: 'error' });
      return;
    }

    if (!symbol) {
      showToast({ title: 'Symbol is required', type: 'error' });
      return;
    }

    const decimalsValue = draft.decimals.trim();
    const parsedDecimals = decimalsValue ? Number(decimalsValue) : undefined;
    if (decimalsValue && (parsedDecimals === undefined || Number.isNaN(parsedDecimals) || parsedDecimals < 0)) {
      showToast({ title: 'Decimals must be a non-negative number', type: 'error' });
      return;
    }
    const decimals = decimalsValue ? parsedDecimals : undefined;

    const baseFeeValue = draft.baseFee.trim();
    const parsedBaseFee = baseFeeValue ? Number(baseFeeValue) : undefined;
    if (baseFeeValue && (parsedBaseFee === undefined || Number.isNaN(parsedBaseFee))) {
      showToast({ title: 'Base fee must be a number', type: 'error' });
      return;
    }
    const baseFee = baseFeeValue ? parsedBaseFee : undefined;

    const result = await createAsset.mutateAsync({
      assetId,
      name,
      symbol,
      decimals,
      type: draft.type.trim() || undefined,
      blockchainType: draft.blockchainType,
      contractAddress: draft.contractAddress.trim() || undefined,
      nativeAsset: draft.nativeAsset.trim() || undefined,
      baseFee,
      feeAssetId: draft.feeAssetId.trim() || undefined,
      isActive: draft.isActive,
    });

    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    setDraft({ ...emptyDraft });
    showToast({ title: 'Asset created', type: 'success', duration: 2500 });
  };

  const handleSave = async (updates: {
    name: string;
    symbol: string;
    decimals?: number;
    type?: string;
    blockchainType: BlockchainType;
    contractAddress?: string;
    nativeAsset?: string;
    baseFee?: number;
    feeAssetId?: string;
    isActive: boolean;
  }) => {
    if (!selectedAsset) return;

    if (!updates.name) {
      showToast({ title: 'Asset name is required', type: 'error' });
      return;
    }
    if (!updates.symbol) {
      showToast({ title: 'Symbol is required', type: 'error' });
      return;
    }

    const result = await updateAsset.mutateAsync({
      id: selectedAsset.id,
      request: updates,
    });

    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    setSelectedAsset(null);
    showToast({ title: 'Asset updated', type: 'success', duration: 2500 });
  };

  const handleDeactivate = async () => {
    if (!selectedAsset) return;

    const confirmed = confirm(`Deactivate asset ${selectedAsset.id}?`);
    if (!confirmed) return;

    const result = await deleteAsset.mutateAsync(selectedAsset.id);
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    setSelectedAsset(null);
    showToast({ title: 'Asset deactivated', type: 'success', duration: 2500 });
  };

  if (isLoading) return <div className="p-8 text-center text-muted">Loading assets...</div>;
  if (error) return <div className="p-8 text-center text-red-500">Error: {error.message}</div>;

  return (
    <div>
      <div className="flex-between mb-4">
        <h2>Assets <span className="text-muted text-sm">({assets?.length || 0})</span></h2>
      </div>

        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleCreate();
          }}
          className="card mb-6"
        >
          <h3 className="mb-4 text-lg font-semibold">Create Asset</h3>
          <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))' }}>
            <input
              type="text"
              placeholder="Asset ID (e.g. BTC)"
              value={draft.assetId}
              onChange={(e) => setDraft((prev) => ({ ...prev, assetId: e.target.value }))}
            />
            <input
              type="text"
              placeholder="Name"
              value={draft.name}
              onChange={(e) => setDraft((prev) => ({ ...prev, name: e.target.value }))}
            />
            <input
              type="text"
              placeholder="Symbol"
              value={draft.symbol}
              onChange={(e) => setDraft((prev) => ({ ...prev, symbol: e.target.value }))}
            />
            <input
              type="number"
              placeholder="Decimals"
              value={draft.decimals}
              min={0}
              onChange={(e) => setDraft((prev) => ({ ...prev, decimals: e.target.value }))}
            />
            <input
              type="text"
              placeholder="Type (BASE_ASSET, ERC20...)"
              value={draft.type}
              onChange={(e) => setDraft((prev) => ({ ...prev, type: e.target.value }))}
            />
            <select
              value={draft.blockchainType}
              onChange={(e) => setDraft((prev) => ({ ...prev, blockchainType: e.target.value as BlockchainType }))}
            >
              {blockchainOptions.map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
            <input
              type="text"
              placeholder="Contract address"
              value={draft.contractAddress}
              onChange={(e) => setDraft((prev) => ({ ...prev, contractAddress: e.target.value }))}
            />
            <input
              type="text"
              placeholder="Native asset"
              value={draft.nativeAsset}
              onChange={(e) => setDraft((prev) => ({ ...prev, nativeAsset: e.target.value }))}
            />
            <input
              type="number"
              placeholder="Base fee"
              step="any"
              value={draft.baseFee}
              onChange={(e) => setDraft((prev) => ({ ...prev, baseFee: e.target.value }))}
            />
            <input
              type="text"
              placeholder="Fee asset ID"
              value={draft.feeAssetId}
              onChange={(e) => setDraft((prev) => ({ ...prev, feeAssetId: e.target.value }))}
            />
            <label className="toggle" style={{ alignSelf: 'center' }}>
              <input
                type="checkbox"
                checked={draft.isActive}
                onChange={(e) => setDraft((prev) => ({ ...prev, isActive: e.target.checked }))}
              />
              <span className="toggle-track" />
              <span className="toggle-label">Active</span>
            </label>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={createAsset.isPending}
            >
              Create
            </button>
          </div>
        </form>

        <div className="overflow-x-auto">
          <table>
            <thead>
              <tr>
                <th>Asset</th>
                <th>Name</th>
                <th>Symbol</th>
                <th>Decimals</th>
                <th>Type</th>
                <th>Chain</th>
                <th>Status</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {(assets || []).map((asset) => (
                <tr
                  key={asset.id}
                  className={`cursor-pointer hover:bg-tertiary/30 ${selectedAsset?.id === asset.id ? 'bg-tertiary/50' : ''}`}
                  onClick={() => setSelectedAsset(asset)}
                >
                  <td className="text-mono font-bold">{asset.id}</td>
                  <td>{asset.name}</td>
                  <td>{asset.symbol}</td>
                  <td>{asset.decimals}</td>
                  <td>{asset.type || '-'}</td>
                  <td>{asset.blockchainType}</td>
                  <td>
                    <span className={`inline-block px-2 py-0.5 text-xs rounded ${asset.isActive ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'}`}>
                      {asset.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="text-sm text-muted">{new Date(asset.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

      {selectedAsset && (
        <AssetEditPanel
          asset={selectedAsset}
          onSave={handleSave}
          onDeactivate={handleDeactivate}
          onClose={() => setSelectedAsset(null)}
          isSaving={updateAsset.isPending}
          isDeactivating={deleteAsset.isPending}
        />
      )}
    </div>
  );
}
