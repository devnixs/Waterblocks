import { useState, useEffect } from 'react';
import type { AdminAsset, BlockchainType } from '../../types/admin';

const blockchainOptions: BlockchainType[] = ['AccountBased', 'AddressBased', 'MemoBased'];

type AssetEditPanelProps = {
  asset: AdminAsset;
  onSave: (updates: {
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
  }) => void;
  onDeactivate: () => void;
  onClose: () => void;
  isSaving: boolean;
  isDeactivating: boolean;
};

export function AssetEditPanel({
  asset,
  onSave,
  onDeactivate,
  onClose,
  isSaving,
  isDeactivating,
}: AssetEditPanelProps) {
  const [name, setName] = useState(asset.name);
  const [symbol, setSymbol] = useState(asset.symbol);
  const [decimals, setDecimals] = useState(String(asset.decimals));
  const [type, setType] = useState(asset.type || '');
  const [blockchainType, setBlockchainType] = useState<BlockchainType>(asset.blockchainType);
  const [contractAddress, setContractAddress] = useState(asset.contractAddress || '');
  const [nativeAsset, setNativeAsset] = useState(asset.nativeAsset || '');
  const [baseFee, setBaseFee] = useState(String(asset.baseFee));
  const [feeAssetId, setFeeAssetId] = useState(asset.feeAssetId || '');
  const [isActive, setIsActive] = useState(asset.isActive);

  useEffect(() => {
    setName(asset.name);
    setSymbol(asset.symbol);
    setDecimals(String(asset.decimals));
    setType(asset.type || '');
    setBlockchainType(asset.blockchainType);
    setContractAddress(asset.contractAddress || '');
    setNativeAsset(asset.nativeAsset || '');
    setBaseFee(String(asset.baseFee));
    setFeeAssetId(asset.feeAssetId || '');
    setIsActive(asset.isActive);
  }, [asset]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const parsedDecimals = decimals.trim() ? Number(decimals) : undefined;
    const parsedBaseFee = baseFee.trim() ? Number(baseFee) : undefined;

    onSave({
      name: name.trim(),
      symbol: symbol.trim().toUpperCase(),
      decimals: parsedDecimals,
      type: type.trim() || undefined,
      blockchainType,
      contractAddress: contractAddress.trim() || undefined,
      nativeAsset: nativeAsset.trim() || undefined,
      baseFee: parsedBaseFee,
      feeAssetId: feeAssetId.trim() || undefined,
      isActive,
    });
  };

  return (
    <div className="detail-panel">
      <div className="detail-panel-header">
        <h2>Edit Asset</h2>
        <button className="close-btn" onClick={onClose}>x</button>
      </div>

      <div className="mb-6">
        <div className="text-mono text-lg font-bold">{asset.id}</div>
        <div className="text-sm text-muted">
          Created: {new Date(asset.createdAt).toLocaleString()}
        </div>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="mb-8">
          <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Basic Information</h3>
          <div className="grid gap-4">
            <div>
              <label className="block text-sm text-muted mb-1">Name</label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Asset name"
                required
              />
            </div>
            <div>
              <label className="block text-sm text-muted mb-1">Symbol</label>
              <input
                type="text"
                value={symbol}
                onChange={(e) => setSymbol(e.target.value)}
                placeholder="Symbol (e.g. BTC)"
                required
              />
            </div>
            <div>
              <label className="block text-sm text-muted mb-1">Decimals</label>
              <input
                type="number"
                value={decimals}
                onChange={(e) => setDecimals(e.target.value)}
                min={0}
                placeholder="18"
              />
            </div>
            <div>
              <label className="block text-sm text-muted mb-1">Type</label>
              <input
                type="text"
                value={type}
                onChange={(e) => setType(e.target.value)}
                placeholder="BASE_ASSET, ERC20, etc."
              />
            </div>
          </div>
        </div>

        <div className="mb-8">
          <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Blockchain Configuration</h3>
          <div className="grid gap-4">
            <div>
              <label className="block text-sm text-muted mb-1">Blockchain Type</label>
              <select
                value={blockchainType}
                onChange={(e) => setBlockchainType(e.target.value as BlockchainType)}
              >
                {blockchainOptions.map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm text-muted mb-1">Contract Address</label>
              <input
                type="text"
                value={contractAddress}
                onChange={(e) => setContractAddress(e.target.value)}
                placeholder="0x..."
                className="font-mono text-sm"
              />
            </div>
            <div>
              <label className="block text-sm text-muted mb-1">Native Asset</label>
              <input
                type="text"
                value={nativeAsset}
                onChange={(e) => setNativeAsset(e.target.value)}
                placeholder="e.g. ETH for ERC20 tokens"
              />
            </div>
          </div>
        </div>

        <div className="mb-8">
          <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Fee Configuration</h3>
          <div className="grid gap-4">
            <div>
              <label className="block text-sm text-muted mb-1">Base Fee</label>
              <input
                type="number"
                step="any"
                value={baseFee}
                onChange={(e) => setBaseFee(e.target.value)}
                placeholder="0"
              />
            </div>
            <div>
              <label className="block text-sm text-muted mb-1">Fee Asset ID</label>
              <input
                type="text"
                value={feeAssetId}
                onChange={(e) => setFeeAssetId(e.target.value)}
                placeholder="Asset used to pay fees"
              />
            </div>
          </div>
        </div>

        <div className="mb-8">
          <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Status</h3>
          <label className="toggle">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
            />
            <span className="toggle-track" />
            <span className="toggle-label">{isActive ? 'Active' : 'Inactive'}</span>
          </label>
        </div>

        <div className="flex gap-2">
          <button
            type="submit"
            className="btn btn-primary flex-1"
            disabled={isSaving}
          >
            {isSaving ? 'Saving...' : 'Save Changes'}
          </button>
          <button
            type="button"
            className="btn btn-danger"
            onClick={onDeactivate}
            disabled={isDeactivating || !asset.isActive}
          >
            {isDeactivating ? 'Deactivating...' : 'Deactivate'}
          </button>
        </div>
      </form>
    </div>
  );
}
