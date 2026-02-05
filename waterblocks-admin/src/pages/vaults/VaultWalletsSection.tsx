import type { AdminVault, Asset } from '../../types/admin';

type VaultWalletsSectionProps = {
  vault: AdminVault;
  assets: Asset[];
  walletAssetId: string;
  setWalletAssetId: (value: string) => void;
  onCreateWallet: () => void;
  isCreatingWallet: boolean;
};

export function VaultWalletsSection({
  vault,
  assets,
  walletAssetId,
  setWalletAssetId,
  onCreateWallet,
  isCreatingWallet,
}: VaultWalletsSectionProps) {
  return (
    <div className="mb-8">
      <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">
        Assets <span className="text-muted">({vault.wallets.length})</span>
      </h3>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          onCreateWallet();
        }}
        className="flex gap-2 mb-4"
      >
        <select
          value={walletAssetId}
          onChange={(e) => setWalletAssetId(e.target.value)}
          className="flex-1"
        >
          <option value="">Select asset</option>
          {assets.map((asset) => (
            <option key={asset.id} value={asset.id}>
              {asset.name} ({asset.symbol})
            </option>
          ))}
        </select>
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isCreatingWallet}
        >
          + Wallet
        </button>
      </form>

      {vault.wallets.length > 0 ? (
        <div className="overflow-x-auto rounded-lg border border-tertiary">
          <table className="w-full text-sm">
            <thead>
              <tr>
                <th>Asset</th>
                <th>Balance</th>
                <th>Locked</th>
                <th>Available</th>
                <th>Type</th>
                <th>Address</th>
                <th>Tag</th>
                <th>Description</th>
              </tr>
            </thead>
            <tbody>
              {vault.wallets.flatMap((wallet) =>
                wallet.addresses.length > 0
                  ? wallet.addresses.map((address, index) => (
                      <tr key={`${wallet.assetId}-${address.id}`}>
                        {index === 0 && (
                          <>
                            <td className="font-bold" rowSpan={wallet.addresses.length}>
                              {wallet.assetId}
                            </td>
                            <td className="text-mono" rowSpan={wallet.addresses.length}>
                              {parseFloat(wallet.balance).toFixed(8)}
                            </td>
                            <td
                              className="text-mono"
                              rowSpan={wallet.addresses.length}
                              style={{
                                color: parseFloat(wallet.lockedAmount) > 0 ? 'var(--warning)' : 'inherit',
                                fontWeight: parseFloat(wallet.lockedAmount) > 0 ? 'bold' : 'normal',
                              }}
                            >
                              {parseFloat(wallet.lockedAmount).toFixed(8)}
                            </td>
                            <td className="text-mono text-success" rowSpan={wallet.addresses.length}>
                              {parseFloat(wallet.available).toFixed(8)}
                            </td>
                          </>
                        )}
                        <td className="font-medium">
                          <span
                            className="inline-block px-2 py-0.5 text-xs rounded"
                            style={{
                              backgroundColor: address.type === 'Permanent' ? 'var(--accent-bg)' : 'var(--secondary-bg)',
                              color: address.type === 'Permanent' ? 'var(--accent)' : 'var(--text-secondary)',
                            }}
                          >
                            {address.type}
                          </span>
                        </td>
                        <td className="text-mono text-sm">{address.addressValue}</td>
                        <td className="text-mono text-xs text-muted">{address.tag || '-'}</td>
                        <td className="text-xs text-muted">{address.description || '-'}</td>
                      </tr>
                    ))
                  : [
                      <tr key={wallet.assetId}>
                        <td className="font-bold">{wallet.assetId}</td>
                        <td className="text-mono">{parseFloat(wallet.balance).toFixed(8)}</td>
                        <td
                          className="text-mono"
                          style={{
                            color: parseFloat(wallet.lockedAmount) > 0 ? 'var(--warning)' : 'inherit',
                            fontWeight: parseFloat(wallet.lockedAmount) > 0 ? 'bold' : 'normal',
                          }}
                        >
                          {parseFloat(wallet.lockedAmount).toFixed(8)}
                        </td>
                        <td className="text-mono text-success">{parseFloat(wallet.available).toFixed(8)}</td>
                        <td colSpan={4} className="text-center text-muted">
                          No addresses
                        </td>
                      </tr>,
                    ]
              )}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="p-4 text-center text-muted border border-dashed border-tertiary rounded-lg">
          No assets in this vault
        </div>
      )}
    </div>
  );
}
