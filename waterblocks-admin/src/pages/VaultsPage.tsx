import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  useVaults,
  useCreateVault,
  useFrozenBalances,
  useCreateWallet,
  useAssets,
  useUpdateVault,
  useArchiveVault,
  useUnarchiveVault,
} from '../api/queries';
import { useToast } from '../components/ToastProvider';
import type { AdminVault } from '../types/admin';
import { CreateVaultForm } from './vaults/CreateVaultForm';
import { VaultDetailPanel } from './vaults/VaultDetailPanel';
import { VaultsHeader } from './vaults/VaultsHeader';
import { VaultsTable } from './vaults/VaultsTable';

export default function VaultsPage() {
  const [showArchived, setShowArchived] = useState(false);
  const { data: vaults, isLoading, error } = useVaults(showArchived);
  const { data: assets } = useAssets();
  const createVault = useCreateVault();
  const updateVault = useUpdateVault();
  const archiveVault = useArchiveVault();
  const unarchiveVault = useUnarchiveVault();
  const { showToast } = useToast();
  const [searchParams] = useSearchParams();
  const [selectedVault, setSelectedVault] = useState<AdminVault | null>(null);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [vaultName, setVaultName] = useState('');
  const [walletAssetId, setWalletAssetId] = useState('');
  const frozenBalancesQuery = useFrozenBalances(selectedVault?.id ?? '');
  const createWallet = useCreateWallet(selectedVault?.id ?? '');
  const displayedVaults = (vaults || []).filter((vault) => (showArchived ? vault.isArchived : !vault.isArchived));

  useEffect(() => {
    if (!selectedVault) return;
    const updated = displayedVaults.find((vault) => vault.id === selectedVault.id);
    if (updated) {
      setSelectedVault(updated);
      return;
    }
    setSelectedVault(null);
  }, [displayedVaults, selectedVault]);

  useEffect(() => {
    if (!displayedVaults || displayedVaults.length === 0) return;
    const vaultId = searchParams.get('vaultId');
    const vaultNameParam = searchParams.get('vaultName');
    if (!vaultId && !vaultNameParam) return;

    const match = vaultId
      ? displayedVaults.find((vault) => vault.id === vaultId)
      : displayedVaults.find((vault) => vault.name === vaultNameParam);

    if (match && match.id !== selectedVault?.id) {
      setSelectedVault(match);
    }
  }, [displayedVaults, searchParams, selectedVault]);

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
    const confirmed = confirm(`Archive vault "${selectedVault.name}"? You can restore it later via the API.`);
    if (!confirmed) return;

    const result = await archiveVault.mutateAsync(selectedVault.id);
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    showToast({ title: 'Vault archived', type: 'success', duration: 3000 });
    setSelectedVault(null);
  };

  const handleUnarchiveVault = async () => {
    if (!selectedVault) return;
    const result = await unarchiveVault.mutateAsync(selectedVault.id);
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    showToast({ title: 'Vault unarchived', type: 'success', duration: 3000 });
    setSelectedVault(null);
  };

  return (
    <div>
      <VaultsHeader
        totalCount={displayedVaults.length}
        showCreateForm={showCreateForm}
        onToggleCreate={() => setShowCreateForm((prev) => !prev)}
        showArchived={showArchived}
        onToggleShowArchived={() => setShowArchived((prev) => !prev)}
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
        vaults={displayedVaults}
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
          onUnarchive={handleUnarchiveVault}
          isRenaming={updateVault.isPending}
          isDeleting={archiveVault.isPending}
          isUnarchiving={unarchiveVault.isPending}
          onClose={() => setSelectedVault(null)}
          frozenBalances={frozenBalancesQuery.data}
          frozenLoading={frozenBalancesQuery.isLoading}
          frozenError={frozenBalancesQuery.error}
        />
      )}
    </div>
  );
}
