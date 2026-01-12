import { useEffect, useState } from 'react';
import { useVaults, useCreateVault, useFrozenBalances, useCreateWallet, useAssets } from '../api/queries';
import { useToast } from '../components/ToastProvider';
import type { AdminVault } from '../types/admin';

export default function VaultsPage() {
  const { data: vaults, isLoading, error } = useVaults();
  const { data: assets } = useAssets();
  const createVault = useCreateVault();
  const { showToast } = useToast();
  const [selectedVault, setSelectedVault] = useState<AdminVault | null>(null);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [vaultName, setVaultName] = useState('');
  const [walletAssetId, setWalletAssetId] = useState('');
  const frozenBalancesQuery = useFrozenBalances(selectedVault?.id ?? '');
  const createWallet = useCreateWallet(selectedVault?.id ?? '');

  useEffect(() => {
    if (!selectedVault || !vaults) return;
    const updated = vaults.find((vault) => vault.id === selectedVault.id);
    if (updated) {
      setSelectedVault(updated);
    }
  }, [vaults, selectedVault]);

  useEffect(() => {
    if (!assets || assets.length === 0) return;
    if (!walletAssetId) {
      setWalletAssetId(assets[0].id);
    }
  }, [assets, walletAssetId]);

  if (isLoading) return <div className="p-8 text-center text-muted">Loading vaults...</div>;
  if (error) return <div className="p-8 text-center text-red-500">Error: {error.message}</div>;

  const handleCreateVault = async () => {
    if (!vaultName.trim()) {
      showToast({ title: 'Vault name is required', type: 'error' });
      return;
    }

    const result = await createVault.mutateAsync({ name: vaultName });
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
    } else {
      showToast({ title: 'Vault created successfully', type: 'success', duration: 3000 });
      setVaultName('');
      setShowCreateForm(false);
    }
  };

  const handleCreateWallet = async () => {
    if (!selectedVault) return;
    if (!walletAssetId.trim()) {
      showToast({ title: 'Asset is required', type: 'error' });
      return;
    }

    const result = await createWallet.mutateAsync({ assetId: walletAssetId.trim().toUpperCase() });
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
    } else {
      showToast({ title: 'Wallet created successfully', type: 'success', duration: 3000 });
      setWalletAssetId('');
    }
  };

  return (
    <div>
      <div className="flex-between mb-4">
        <h2>Vaults <span className="text-muted text-sm">({vaults?.length || 0})</span></h2>
        <button
          className="btn btn-primary"
          onClick={() => setShowCreateForm(!showCreateForm)}
        >
          {showCreateForm ? 'Cancel' : '+ Create Vault'}
        </button>
      </div>

      {showCreateForm && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleCreateVault();
          }}
          className="card"
        >
          <h3 className="mb-4 text-lg font-semibold">Create New Vault</h3>
          <div className="flex gap-2">
            <input
              type="text"
              placeholder="Vault name"
              value={vaultName}
              onChange={(e) => setVaultName(e.target.value)}
              className="flex-1"
            />
            <button
              type="submit"
              className="btn btn-primary"
              disabled={createVault.isPending}
            >
              Create
            </button>
          </div>
        </form>
      )}

      <div className="overflow-x-auto rounded-lg border border-tertiary">
        <table className="w-full">
          <thead>
            <tr>
              <th>ID</th>
              <th>Name</th>
              <th>Assets</th>
              <th>Hidden</th>
              <th>Created</th>
              <th className="text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {vaults?.map((vault) => (
              <tr
                key={vault.id}
                onClick={() => setSelectedVault(vault)}
                className="cursor-pointer hover:bg-white/5 transition-colors"
              >
                <td className="text-mono text-sm text-muted">
                  {vault.id.substring(0, 8)}...
                </td>
                <td className="font-medium">{vault.name}</td>
                <td className="text-mono">{vault.wallets.length}</td>
                <td>{vault.hiddenOnUI ? 'Yes' : 'No'}</td>
                <td className="text-sm text-muted">{new Date(vault.createdAt).toLocaleString()}</td>
                <td className="text-right">
                  <button
                    className="btn btn-ghost text-sm py-1 px-3"
                    onClick={(e) => {
                      e.stopPropagation();
                      setSelectedVault(vault);
                    }}
                  >
                    View
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {selectedVault && (
        <div className="detail-panel">
          <div className="detail-panel-header">
            <h2>Vault Details</h2>
            <button className="close-btn" onClick={() => setSelectedVault(null)}>×</button>
          </div>

          <div className="mb-8">
            <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Information</h3>
            <div className="grid gap-4 p-4 bg-tertiary/20 rounded-lg border border-tertiary">
              <div className="flex justify-between">
                <span className="text-muted">ID</span>
                <span className="text-mono select-all">{selectedVault.id}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted">Name</span>
                <span className="font-medium">{selectedVault.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted">Hidden</span>
                <span>{selectedVault.hiddenOnUI ? 'Yes' : 'No'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted">Auto Fuel</span>
                <span>{selectedVault.autoFuel ? 'Yes' : 'No'}</span>
              </div>
              {selectedVault.customerRefId && (
                <div className="flex justify-between">
                  <span className="text-muted">Customer Ref</span>
                  <span className="text-mono">{selectedVault.customerRefId}</span>
                </div>
              )}
              <div className="flex justify-between">
                <span className="text-muted">Created</span>
                <span className="text-sm">{new Date(selectedVault.createdAt).toLocaleString()}</span>
              </div>
            </div>
          </div>

          <div className="mb-8">
            <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">
              Assets <span className="text-muted">({selectedVault.wallets.length})</span>
            </h3>

            <form
              onSubmit={(e) => {
                e.preventDefault();
                handleCreateWallet();
              }}
              className="flex gap-2 mb-4"
            >
              <select
                value={walletAssetId}
                onChange={(e) => setWalletAssetId(e.target.value)}
                className="flex-1"
              >
                <option value="">Select asset</option>
                {(assets || []).map((asset) => (
                  <option key={asset.id} value={asset.id}>
                    {asset.name} ({asset.symbol})
                  </option>
                ))}
              </select>
              <button
                type="submit"
                className="btn btn-primary"
                disabled={createWallet.isPending}
              >
                + Wallet
              </button>
            </form>

            {selectedVault.wallets.length > 0 ? (
              <div className="overflow-x-auto rounded-lg border border-tertiary">
                <table className="w-full text-sm">
                  <thead>
                    <tr>
                      <th>Asset</th>
                      <th>Balance</th>
                      <th>Locked</th>
                      <th>Available</th>
                      <th>Deposit Address</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedVault.wallets.map((wallet) => (
                      <tr key={wallet.assetId}>
                        <td className="font-bold">{wallet.assetId}</td>
                        <td className="text-mono">{parseFloat(wallet.balance).toFixed(8)}</td>
                        <td className="text-mono" style={{
                          color: parseFloat(wallet.lockedAmount) > 0 ? 'var(--warning)' : 'inherit',
                          fontWeight: parseFloat(wallet.lockedAmount) > 0 ? 'bold' : 'normal'
                        }}>
                          {parseFloat(wallet.lockedAmount).toFixed(8)}
                        </td>
                        <td className="text-mono text-success">{parseFloat(wallet.available).toFixed(8)}</td>
                        <td className="text-mono">
                          {wallet.depositAddress ? wallet.depositAddress : '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="p-4 text-center text-muted border border-dashed border-tertiary rounded-lg">
                No assets in this vault
              </div>
            )}
          </div>

          <div className="mb-8">
            <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Frozen Balances</h3>
            {frozenBalancesQuery.isLoading && <p className="text-muted">Loading frozen balances...</p>}
            {frozenBalancesQuery.error && (
              <p className="text-danger">Error: {String(frozenBalancesQuery.error)}</p>
            )}
            {!frozenBalancesQuery.isLoading && !frozenBalancesQuery.error && (
              frozenBalancesQuery.data && frozenBalancesQuery.data.length > 0 ? (
                <div className="overflow-x-auto rounded-lg border border-tertiary">
                  <table className="w-full text-sm">
                    <thead>
                      <tr>
                        <th>Asset</th>
                        <th>Amount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {frozenBalancesQuery.data.map((balance) => (
                        <tr key={balance.assetId}>
                          <td className="font-bold">{balance.assetId}</td>
                          <td className="text-mono">{parseFloat(balance.amount).toFixed(8)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="p-4 text-center text-muted border border-dashed border-tertiary rounded-lg">
                  No frozen balances
                </div>
              )
            )}
          </div>

          <div className="mt-8">
            <details>
              <summary className="cursor-pointer text-xs font-bold uppercase tracking-wider text-muted mb-2">Raw JSON</summary>
              <pre className="bg-black/50 p-4 rounded-lg overflow-auto text-xs font-mono border border-tertiary">
                {JSON.stringify(selectedVault, null, 2)}
              </pre>
            </details>
          </div>
        </div>
      )}
    </div>
  );
}
