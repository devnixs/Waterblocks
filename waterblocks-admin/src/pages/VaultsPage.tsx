import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useVaults, useCreateVault, useFrozenBalances, useCreateWallet, useAssets, useUpdateVault, useDeleteVault } from '../api/queries';
import { useToast } from '../components/ToastProvider';
import type { AdminVault } from '../types/admin';
import { CreateVaultForm } from './vaults/CreateVaultForm';
import { VaultDetailPanel } from './vaults/VaultDetailPanel';
import { VaultsHeader } from './vaults/VaultsHeader';
import { VaultsTable } from './vaults/VaultsTable';

export default function VaultsPage() {
  const { data: vaults, isLoading, error } = useVaults();
  const { data: assets } = useAssets();
  const createVault = useCreateVault();
  const updateVault = useUpdateVault();
  const deleteVault = useDeleteVault();
  const { showToast } = useToast();
  const [searchParams] = useSearchParams();
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
    if (!vaults || vaults.length === 0) return;
    const vaultId = searchParams.get('vaultId');
    const vaultNameParam = searchParams.get('vaultName');
    if (!vaultId && !vaultNameParam) return;

    const match = vaultId
      ? vaults.find((vault) => vault.id === vaultId)
      : vaults.find((vault) => vault.name === vaultNameParam);

    if (match && match.id !== selectedVault?.id) {
      setSelectedVault(match);
    }
  }, [vaults, searchParams, selectedVault]);

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

  const handleRenameVault = async () => {
    if (!selectedVault) return;
    const nextName = prompt('Enter new vault name:', selectedVault.name);
    if (!nextName || !nextName.trim()) return;

    const result = await updateVault.mutateAsync({ id: selectedVault.id, request: { name: nextName.trim() } });
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    showToast({ title: 'Vault renamed', type: 'success', duration: 3000 });
  };

  const handleDeleteVault = async () => {
    if (!selectedVault) return;
    const confirmed = confirm(`Delete vault "${selectedVault.name}"? This cannot be undone.`);
    if (!confirmed) return;

    const result = await deleteVault.mutateAsync(selectedVault.id);
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    showToast({ title: 'Vault deleted', type: 'success', duration: 3000 });
    setSelectedVault(null);
  };

  return (
    <div>
      <VaultsHeader
        totalCount={vaults?.length || 0}
        showCreateForm={showCreateForm}
        onToggleCreate={() => setShowCreateForm((prev) => !prev)}
      />

      {showCreateForm && (
        <CreateVaultForm
          vaultName={vaultName}
          setVaultName={setVaultName}
          onSubmit={handleCreateVault}
          isSubmitting={createVault.isPending}
        />
      )}

      <VaultsTable
        vaults={vaults || []}
        onSelect={setSelectedVault}
      />

      {selectedVault && (
        <VaultDetailPanel
          vault={selectedVault}
          assets={assets || []}
          walletAssetId={walletAssetId}
          setWalletAssetId={setWalletAssetId}
          onCreateWallet={handleCreateWallet}
          isCreatingWallet={createWallet.isPending}
          onRename={handleRenameVault}
          onDelete={handleDeleteVault}
          isRenaming={updateVault.isPending}
          isDeleting={deleteVault.isPending}
          onClose={() => setSelectedVault(null)}
          frozenBalances={frozenBalancesQuery.data}
          frozenLoading={frozenBalancesQuery.isLoading}
          frozenError={frozenBalancesQuery.error}
        />
      )}
    </div>
  );
}
