import type { AdminVault, Asset } from '../../types/admin';
import { FrozenBalancesSection } from './FrozenBalancesSection';
import { VaultInfoSection } from './VaultInfoSection';
import { VaultRawJson } from './VaultRawJson';
import { VaultWalletsSection } from './VaultWalletsSection';

type VaultDetailPanelProps = {
  vault: AdminVault;
  assets: Asset[];
  walletAssetId: string;
  setWalletAssetId: (value: string) => void;
  onCreateWallet: () => void;
  isCreatingWallet: boolean;
  onRename: () => void;
  onDelete: () => void;
  isRenaming: boolean;
  isDeleting: boolean;
  onClose: () => void;
  frozenBalances?: { assetId: string; amount: string }[];
  frozenLoading: boolean;
  frozenError: unknown;
};

export function VaultDetailPanel({
  vault,
  assets,
  walletAssetId,
  setWalletAssetId,
  onCreateWallet,
  isCreatingWallet,
  onRename,
  onDelete,
  isRenaming,
  isDeleting,
  onClose,
  frozenBalances,
  frozenLoading,
  frozenError,
}: VaultDetailPanelProps) {
  return (
    <div className="detail-panel">
      <div className="detail-panel-header">
        <h2>Vault Details</h2>
        <button className="close-btn" onClick={onClose}>x</button>
      </div>

      <div className="flex gap-2 mb-6">
        <button
          className="btn btn-secondary"
          onClick={onRename}
          disabled={isRenaming}
        >
          Rename
        </button>
        <button
          className="btn btn-danger"
          onClick={onDelete}
          disabled={isDeleting}
        >
          Delete
        </button>
      </div>

      <VaultInfoSection vault={vault} />
      <VaultWalletsSection
        vault={vault}
        assets={assets}
        walletAssetId={walletAssetId}
        setWalletAssetId={setWalletAssetId}
        onCreateWallet={onCreateWallet}
        isCreatingWallet={isCreatingWallet}
      />
      <FrozenBalancesSection
        balances={frozenBalances}
        isLoading={frozenLoading}
        error={frozenError}
      />
      <VaultRawJson vault={vault} />
    </div>
  );
}
