import type { Asset, AdminVault, EstimateFeeResponse } from '../../types/admin';
import type { SetState, TransactionEndpointType } from './types';
import { SearchableVaultSelect } from '../../components/SearchableVaultSelect';

export type FeeLevel = 'LOW' | 'MEDIUM' | 'HIGH';

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
  hash: string;
  setHash: SetState<string>;
  feeLevel: FeeLevel;
  setFeeLevel: SetState<FeeLevel>;
  treatAsGrossAmount: boolean;
  setTreatAsGrossAmount: SetState<boolean>;
  feeEstimates: EstimateFeeResponse | null | undefined;
  feeEstimatesLoading: boolean;
  onSubmit: () => void;
  onCancel: () => void;
  isSubmitting: boolean;
};

function formatFee(fee: string | undefined): string {
  if (!fee) return '0';
  const num = parseFloat(fee);
  if (isNaN(num)) return '0';
  if (num === 0) return '0';
  if (num < 0.000001) return num.toExponential(2);
  return num.toFixed(8).replace(/\.?0+$/, '');
}

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
  hash,
  setHash,
  feeLevel,
  setFeeLevel,
  treatAsGrossAmount,
  setTreatAsGrossAmount,
  feeEstimates,
  feeEstimatesLoading,
  onSubmit,
  onCancel,
  isSubmitting,
}: CreateTransactionFormProps) {
  const selectedAsset = assets.find((a) => a.id === assetId);
  const symbol = selectedAsset?.symbol || assetId || '';

  const getNetworkFee = (level: FeeLevel): string => {
    if (!feeEstimates) return '0';
    const estimate = feeEstimates[level.toLowerCase() as 'low' | 'medium' | 'high'];
    return estimate?.networkFee || '0';
  };

  const selectedFee = getNetworkFee(feeLevel);
  const parsedAmount = parseFloat(amount) || 0;
  const parsedFee = parseFloat(selectedFee) || 0;

  const recipientReceives = treatAsGrossAmount
    ? Math.max(0, parsedAmount - parsedFee)
    : parsedAmount;
  const totalCost = treatAsGrossAmount
    ? parsedAmount
    : parsedAmount + parsedFee;

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
                <div className="w-2/3">
                  <SearchableVaultSelect
                    vaults={vaults}
                    selectedVaultId={sourceVaultId}
                    onSelect={setSourceVaultId}
                    placeholder="Search by name, ID, or address..."
                    assetId={assetId}
                  />
                </div>
              )}
            </div>
            {sourceType === 'INTERNAL' && (
              <input
                type="text"
                placeholder="Specific address (optional - uses first if empty)"
                value={sourceAddress}
                onChange={(e) => setSourceAddress(e.target.value)}
                className="text-sm"
              />
            )}
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
                <div className="w-2/3">
                  <SearchableVaultSelect
                    vaults={vaults}
                    selectedVaultId={destinationVaultId}
                    onSelect={setDestinationVaultId}
                    placeholder="Search by name, ID, or address..."
                    assetId={assetId}
                  />
                </div>
              )}
            </div>
            {destinationType === 'INTERNAL' && (
              <input
                type="text"
                placeholder="Specific address (optional - uses first if empty)"
                value={destinationAddress}
                onChange={(e) => setDestinationAddress(e.target.value)}
                className="text-sm"
              />
            )}
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

        <div>
          <label className="block text-sm text-muted mb-1">Fee Level</label>
          {feeEstimatesLoading ? (
            <div className="text-sm text-muted">Loading fee estimates...</div>
          ) : !assetId ? (
            <div className="text-sm text-muted">Select an asset to see fee estimates</div>
          ) : (
            <div className="flex gap-4">
              {(['LOW', 'MEDIUM', 'HIGH'] as const).map((level) => {
                const fee = getNetworkFee(level);
                const isSelected = feeLevel === level;
                return (
                  <label
                    key={level}
                    className={`flex items-center gap-2 p-2 rounded border cursor-pointer transition-colors ${
                      isSelected
                        ? 'border-blue-500 bg-blue-500/10'
                        : 'border-gray-600 hover:border-gray-500'
                    }`}
                  >
                    <input
                      type="radio"
                      name="feeLevel"
                      value={level}
                      checked={isSelected}
                      onChange={() => setFeeLevel(level)}
                      className="sr-only"
                    />
                    <div>
                      <div className={`text-sm font-medium ${isSelected ? 'text-blue-400' : ''}`}>
                        {level.charAt(0) + level.slice(1).toLowerCase()}
                      </div>
                      <div className="text-xs text-muted">
                        {formatFee(fee)} {symbol}
                      </div>
                    </div>
                  </label>
                );
              })}
            </div>
          )}
        </div>

        <div>
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={treatAsGrossAmount}
              onChange={(e) => setTreatAsGrossAmount(e.target.checked)}
              className="rounded"
            />
            <span className="text-sm">Deduct fees from amount</span>
          </label>
          <p className="text-xs text-muted mt-1 ml-6">
            {treatAsGrossAmount
              ? 'Fee will be subtracted from the amount. Recipient receives less.'
              : 'Fee will be added on top. You pay amount + fee.'}
          </p>
        </div>

        {parsedAmount > 0 && assetId && (
          <div className="bg-gray-800 rounded p-3 text-sm">
            <div className="flex justify-between mb-1">
              <span className="text-muted">Amount entered:</span>
              <span>{parsedAmount} {symbol}</span>
            </div>
            <div className="flex justify-between mb-1">
              <span className="text-muted">Network fee:</span>
              <span>{treatAsGrossAmount ? '-' : '+'}{formatFee(selectedFee)} {symbol}</span>
            </div>
            <div className="border-t border-gray-700 my-2" />
            <div className="flex justify-between">
              <span className="text-muted">Recipient receives:</span>
              <span className="font-medium">{formatFee(recipientReceives.toString())} {symbol}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted">Total cost:</span>
              <span className="font-medium">{formatFee(totalCost.toString())} {symbol}</span>
            </div>
          </div>
        )}

        <div>
          <label className="block text-sm text-muted mb-1">
            Transaction Hash (optional)
          </label>
          <input
            type="text"
            placeholder="Leave empty for auto-generation"
            value={hash}
            onChange={(e) => setHash(e.target.value)}
            className="font-mono text-sm"
          />
          <p className="text-xs text-muted mt-1">
            BTC: 64 hex chars (no prefix) • ETH: 0x + 64 hex chars
          </p>
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
