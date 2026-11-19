import { useState } from 'react';
import type { FormEvent } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { useCollateralTransfer, useEstimateFee, useVaults } from '../../api/queries';
import { SearchableVaultSelect } from '../../components/SearchableVaultSelect';
import { useToast } from '../../components/ToastProvider';
import { useCurrentUser } from '../../hooks/useCurrentUser';
import type { AdminExchangeAccount, AdminExchangeAsset, CollateralDirection } from '../../types/admin';
import { TransactionAmountSummary, formatFee } from '../transactions/TransactionAmountSummary';
import { collateralAccountName, trimAmount } from './exchangeFormat';

export type CollateralTransferTarget = {
  asset: AdminExchangeAsset;
  direction: CollateralDirection;
};

type CollateralTransferDialogProps = {
  account: AdminExchangeAccount;
  target: CollateralTransferTarget | null;
  onClose: () => void;
};

type CollateralTransferFormProps = {
  account: AdminExchangeAccount;
  target: CollateralTransferTarget;
  onClose: () => void;
};

function ReadOnlyField({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div>
      <label className="block text-sm text-muted mb-1">{label}</label>
      <input type="text" value={value} aria-label={label} readOnly />
      {hint && <p className="text-xs text-muted mt-1">{hint}</p>}
    </div>
  );
}

function CollateralTransferForm({ account, target, onClose }: CollateralTransferFormProps) {
  const { asset, direction } = target;
  const isAdd = direction === 'ADD';
  const { data: vaults } = useVaults();
  const { data: feeEstimates } = useEstimateFee(asset.assetId);
  const { email } = useCurrentUser();
  const { showToast } = useToast();
  const transfer = useCollateralTransfer(account.id, direction);
  const [vaultAccountId, setVaultAccountId] = useState('');
  const [amount, setAmount] = useState('');
  const [treatAsGrossAmount, setTreatAsGrossAmount] = useState(false);
  const [note, setNote] = useState('');
  const [error, setError] = useState('');

  const networkFee = feeEstimates?.low.networkFee ?? '0';
  const selectedVault = (vaults ?? []).find((vault) => vault.id === vaultAccountId);
  const vaultWallet = selectedVault?.wallets.find((wallet) => wallet.assetId === asset.assetId);
  const vaultAvailable = trimAmount(vaultWallet?.available) || '0';
  const exchangeAvailable = trimAmount(asset.available) || '0';
  const maxAmount = isAdd ? vaultAvailable : exchangeAvailable;
  const collateralName = collateralAccountName(account);

  const vaultSelect = (
    <div>
      <label className="block text-sm text-muted mb-1">{isAdd ? 'From' : 'To'}</label>
      <SearchableVaultSelect
        vaults={vaults ?? []}
        selectedVaultId={vaultAccountId}
        onSelect={setVaultAccountId}
        placeholder="Search by name, ID, or address..."
        assetId={asset.assetId}
      />
      {isAdd && selectedVault && (
        <p className="text-xs text-muted mt-1">Available: {vaultAvailable} {asset.assetId}</p>
      )}
    </div>
  );

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    if (!vaultAccountId) {
      setError('Select a vault');
      return;
    }

    try {
      const response = await transfer.mutateAsync({
        assetId: asset.assetId,
        vaultAccountId,
        amount: amount.trim(),
        treatAsGrossAmount,
        note: note.trim() || undefined,
        initiatedBy: email || undefined,
      });

      if (response.error || !response.data) {
        setError(response.error?.message ?? 'The transfer could not be created');
        return;
      }

      if (response.data.state === 'CANCELLED') {
        showToast({ title: 'The exchange rejected the removal', type: 'error', duration: 5000 });
      } else {
        showToast({ title: 'Collateral transfer created', type: 'success', duration: 3000 });
      }

      onClose();
    } catch {
      setError('The Waterblocks API cannot be reached');
    }
  };

  return (
    <form onSubmit={handleSubmit} className="grid gap-4">
      <ReadOnlyField label="Asset" value={asset.assetId} />
      {isAdd
        ? vaultSelect
        : (
          <ReadOnlyField
            label="From"
            value={collateralName}
            hint={`Available: ${exchangeAvailable} ${asset.assetId}`}
          />
        )}
      {isAdd ? <ReadOnlyField label="To" value={collateralName} /> : vaultSelect}

      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="collateral-amount">Amount</label>
        <div className="input-with-action">
          <input
            id="collateral-amount"
            type="text"
            placeholder="0.00"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            required
          />
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => {
              setAmount(maxAmount);
              setTreatAsGrossAmount(true);
            }}
          >
            Max
          </button>
        </div>
      </div>

      <div>
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={treatAsGrossAmount}
            onChange={(event) => setTreatAsGrossAmount(event.target.checked)}
          />
          <span className="text-sm">Deduct fees from amount</span>
        </label>
        <p className="text-xs text-muted mt-1">
          Network fee: {formatFee(networkFee)} {asset.assetId}
        </p>
      </div>

      <TransactionAmountSummary
        amount={amount}
        networkFee={networkFee}
        symbol={asset.assetId}
        treatAsGrossAmount={treatAsGrossAmount}
      />

      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="collateral-note">Note</label>
        <input
          id="collateral-note"
          type="text"
          placeholder="Optional"
          value={note}
          onChange={(event) => setNote(event.target.value)}
        />
      </div>

      {error && <div className="form-error" role="alert">{error}</div>}

      <div className="flex gap-2 justify-end">
        <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={transfer.isPending}>
          {isAdd ? 'Add collateral' : 'Remove collateral'}
        </button>
      </div>
    </form>
  );
}

export function CollateralTransferDialog({ account, target, onClose }: CollateralTransferDialogProps) {
  return (
    <Dialog.Root open={!!target} onOpenChange={(open) => !open && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content className="dialog-content">
          <Dialog.Title className="text-xl font-semibold mb-2">
            {target?.direction === 'REMOVE' ? 'Remove collateral' : 'Add collateral'}
          </Dialog.Title>
          <Dialog.Description className="text-sm text-muted mb-4">
            Move {target?.asset.assetId} between a vault and the collateral vault account.
          </Dialog.Description>
          {target && <CollateralTransferForm account={account} target={target} onClose={onClose} />}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
