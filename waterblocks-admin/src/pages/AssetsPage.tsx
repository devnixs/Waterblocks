import { useMemo, useState } from 'react';
import { useAdminAssets, useCreateAdminAsset, useDeleteAdminAsset, useUpdateAdminAsset } from '../api/queries';
import type { AdminAsset, BlockchainType } from '../types/admin';
import { useToast } from '../components/ToastProvider';

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
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState({ ...emptyDraft });

  const assetsById = useMemo(() => {
    const map = new Map<string, AdminAsset>();
    (assets || []).forEach((asset) => map.set(asset.id, asset));
    return map;
  }, [assets]);

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
    const decimals = decimalsValue ? Number(decimalsValue) : undefined;
    if (decimalsValue && (Number.isNaN(decimals) || decimals < 0)) {
      showToast({ title: 'Decimals must be a non-negative number', type: 'error' });
      return;
    }

    const baseFeeValue = draft.baseFee.trim();
    const baseFee = baseFeeValue ? Number(baseFeeValue) : undefined;
    if (baseFeeValue && Number.isNaN(baseFee)) {
      showToast({ title: 'Base fee must be a number', type: 'error' });
      return;
    }

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

  const handleEdit = (asset: AdminAsset) => {
    setEditingId(asset.id);
    setEditDraft({
      assetId: asset.id,
      name: asset.name,
      symbol: asset.symbol,
      decimals: String(asset.decimals),
      type: asset.type || '',
      blockchainType: asset.blockchainType,
      contractAddress: asset.contractAddress || '',
      nativeAsset: asset.nativeAsset || '',
      baseFee: asset.baseFee.toString(),
      feeAssetId: asset.feeAssetId || '',
      isActive: asset.isActive,
    });
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setEditDraft({ ...emptyDraft });
  };

  const handleSave = async () => {
    if (!editingId) return;
    const asset = assetsById.get(editingId);
    if (!asset) return;

    const name = editDraft.name.trim();
    const symbol = editDraft.symbol.trim().toUpperCase();
    if (!name) {
      showToast({ title: 'Asset name is required', type: 'error' });
      return;
    }
    if (!symbol) {
      showToast({ title: 'Symbol is required', type: 'error' });
      return;
    }

    const decimalsValue = editDraft.decimals.trim();
    const decimals = decimalsValue ? Number(decimalsValue) : undefined;
    if (decimalsValue && (Number.isNaN(decimals) || decimals < 0)) {
      showToast({ title: 'Decimals must be a non-negative number', type: 'error' });
      return;
    }

    const baseFeeValue = editDraft.baseFee.trim();
    const baseFee = baseFeeValue ? Number(baseFeeValue) : undefined;
    if (baseFeeValue && Number.isNaN(baseFee)) {
      showToast({ title: 'Base fee must be a number', type: 'error' });
      return;
    }

    const result = await updateAsset.mutateAsync({
      id: asset.id,
      request: {
        name,
        symbol,
        decimals,
        type: editDraft.type.trim() || undefined,
        blockchainType: editDraft.blockchainType,
        contractAddress: editDraft.contractAddress.trim() || undefined,
        nativeAsset: editDraft.nativeAsset.trim() || undefined,
        baseFee,
        feeAssetId: editDraft.feeAssetId.trim() || undefined,
        isActive: editDraft.isActive,
      },
    });

    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    setEditingId(null);
    showToast({ title: 'Asset updated', type: 'success', duration: 2500 });
  };

  const handleDelete = async (asset: AdminAsset) => {
    const confirmed = confirm(`Deactivate asset ${asset.id}?`);
    if (!confirmed) return;

    const result = await deleteAsset.mutateAsync(asset.id);
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

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
              <th>Base Fee</th>
              <th>Fee Asset</th>
              <th>Native</th>
              <th>Status</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {(assets || []).map((asset) => {
              const isEditing = editingId === asset.id;
              return (
                <tr key={asset.id}>
                  <td className="text-mono">{asset.id}</td>
                  <td>
                    {isEditing ? (
                      <input
                        type="text"
                        value={editDraft.name}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, name: e.target.value }))}
                      />
                    ) : (
                      asset.name
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <input
                        type="text"
                        value={editDraft.symbol}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, symbol: e.target.value }))}
                      />
                    ) : (
                      asset.symbol
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <input
                        type="number"
                        min={0}
                        value={editDraft.decimals}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, decimals: e.target.value }))}
                      />
                    ) : (
                      asset.decimals
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <input
                        type="text"
                        value={editDraft.type}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, type: e.target.value }))}
                      />
                    ) : (
                      asset.type || '-'
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <select
                        value={editDraft.blockchainType}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, blockchainType: e.target.value as BlockchainType }))}
                      >
                        {blockchainOptions.map((option) => (
                          <option key={option} value={option}>{option}</option>
                        ))}
                      </select>
                    ) : (
                      asset.blockchainType
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <input
                        type="number"
                        step="any"
                        value={editDraft.baseFee}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, baseFee: e.target.value }))}
                      />
                    ) : (
                      asset.baseFee
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <input
                        type="text"
                        value={editDraft.feeAssetId}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, feeAssetId: e.target.value }))}
                      />
                    ) : (
                      asset.feeAssetId || '-'
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <input
                        type="text"
                        value={editDraft.nativeAsset}
                        onChange={(e) => setEditDraft((prev) => ({ ...prev, nativeAsset: e.target.value }))}
                      />
                    ) : (
                      asset.nativeAsset || '-'
                    )}
                  </td>
                  <td>
                    {isEditing ? (
                      <label className="toggle">
                        <input
                          type="checkbox"
                          checked={editDraft.isActive}
                          onChange={(e) => setEditDraft((prev) => ({ ...prev, isActive: e.target.checked }))}
                        />
                        <span className="toggle-track" />
                        <span className="toggle-label">{editDraft.isActive ? 'Active' : 'Inactive'}</span>
                      </label>
                    ) : (
                      asset.isActive ? 'Active' : 'Inactive'
                    )}
                  </td>
                  <td className="text-sm text-muted">{new Date(asset.createdAt).toLocaleString()}</td>
                  <td>
                    {isEditing ? (
                      <div className="flex gap-2">
                        <button className="btn btn-primary" onClick={handleSave} disabled={updateAsset.isPending}>
                          Save
                        </button>
                        <button className="btn btn-secondary" onClick={handleCancelEdit}>
                          Cancel
                        </button>
                      </div>
                    ) : (
                      <div className="flex gap-2">
                        <button className="btn btn-secondary" onClick={() => handleEdit(asset)}>
                          Edit
                        </button>
                        <button className="btn btn-danger" onClick={() => handleDelete(asset)} disabled={deleteAsset.isPending}>
                          Deactivate
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
