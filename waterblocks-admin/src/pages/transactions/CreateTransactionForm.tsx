import type { Asset, AdminVault } from '../../types/admin';
import type { SetState, TransactionEndpointType } from './types';

type CreateTransactionFormProps = {
  assets: Asset[];
  vaults: AdminVault[];
  assetId: string;
  setAssetId: SetState<string>;
  sourceType: TransactionEndpointType;
  setSourceType: SetState<TransactionEndpointType>;
  sourceAddress: string;
  setSourceAddress: SetState<string>;
  sourceVaultId: string;
  setSourceVaultId: SetState<string>;
  destinationType: TransactionEndpointType;
  setDestinationType: SetState<TransactionEndpointType>;
  destinationAddress: string;
  setDestinationAddress: SetState<string>;
  destinationVaultId: string;
  setDestinationVaultId: SetState<string>;
  amount: string;
  setAmount: SetState<string>;
  onSubmit: () => void;
  onCancel: () => void;
  isSubmitting: boolean;
};

export function CreateTransactionForm({
  assets,
  vaults,
  assetId,
  setAssetId,
  sourceType,
  setSourceType,
  sourceAddress,
  setSourceAddress,
  sourceVaultId,
  setSourceVaultId,
  destinationType,
  setDestinationType,
  destinationAddress,
  setDestinationAddress,
  destinationVaultId,
  setDestinationVaultId,
  amount,
  setAmount,
  onSubmit,
  onCancel,
  isSubmitting,
}: CreateTransactionFormProps) {
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit();
      }}
      className="card"
    >
      <h3 className="mb-4 text-lg font-semibold">Create Blockchain Transaction</h3>
      <div className="grid gap-4">
        <div>
          <label className="block text-sm text-muted mb-1">Asset</label>
          <select
            value={assetId}
            onChange={(e) => setAssetId(e.target.value)}
          >
            <option value="">Select asset</option>
            {assets.map((asset) => (
              <option key={asset.id} value={asset.id}>
                {asset.name} ({asset.symbol})
              </option>
            ))}
          </select>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm text-muted mb-1">Source</label>
            <div className="flex gap-2 mb-2">
              <select
                value={sourceType}
                onChange={(e) => setSourceType(e.target.value as TransactionEndpointType)}
                className="w-1/3"
              >
                <option value="EXTERNAL">External</option>
                <option value="INTERNAL">Internal</option>
              </select>
              {sourceType === 'EXTERNAL' ? (
                <input
                  type="text"
                  placeholder="Source address"
                  value={sourceAddress}
                  onChange={(e) => setSourceAddress(e.target.value)}
                  className="w-2/3"
                />
              ) : (
                <select
                  value={sourceVaultId}
                  onChange={(e) => setSourceVaultId(e.target.value)}
                  className="w-2/3"
                >
                  <option value="">Select source vault</option>
                  {vaults.map((vault) => (
                    <option key={vault.id} value={vault.id}>
                      {vault.name} ({vault.id.slice(0, 8)}...)
                    </option>
                  ))}
                </select>
              )}
            </div>
          </div>

          <div>
            <label className="block text-sm text-muted mb-1">Destination</label>
            <div className="flex gap-2 mb-2">
              <select
                value={destinationType}
                onChange={(e) => setDestinationType(e.target.value as TransactionEndpointType)}
                className="w-1/3"
              >
                <option value="EXTERNAL">External</option>
                <option value="INTERNAL">Internal</option>
              </select>
              {destinationType === 'EXTERNAL' ? (
                <input
                  type="text"
                  placeholder="Destination address"
                  value={destinationAddress}
                  onChange={(e) => setDestinationAddress(e.target.value)}
                  className="w-2/3"
                />
              ) : (
                <select
                  value={destinationVaultId}
                  onChange={(e) => setDestinationVaultId(e.target.value)}
                  className="w-2/3"
                >
                  <option value="">Select destination vault</option>
                  {vaults.map((vault) => (
                    <option key={vault.id} value={vault.id}>
                      {vault.name} ({vault.id.slice(0, 8)}...)
                    </option>
                  ))}
                </select>
              )}
            </div>
          </div>
        </div>

        <div>
          <label className="block text-sm text-muted mb-1">Amount</label>
          <input
            type="text"
            placeholder="0.00"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
        </div>
      </div>

      <div className="flex gap-2 mt-6 justify-end">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onCancel}
        >
          Cancel
        </button>
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isSubmitting}
        >
          Create Transaction
        </button>
      </div>
    </form>
  );
}
