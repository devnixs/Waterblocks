import { useState, useEffect, useMemo } from 'react';
import { useTransactionsPaged, useTransitionTransaction, useCreateTransaction, useVaults, useAssets, useEstimateFee, useAdminAssets } from '../api/queries';
import { adminApi } from '../api/adminClient';
import { useToast } from '../components/ToastProvider';
import { useKeyboardShortcuts } from '../hooks/useKeyboardShortcuts';
import { useCurrentUser } from '../hooks/useCurrentUser';
import type { AdminTransaction } from '../types/admin';
import { BulkConfirmDialog } from './transactions/BulkConfirmDialog';
import { CreateTransactionForm, type FeeLevel } from './transactions/CreateTransactionForm';
import { TransactionDetailPanel } from './transactions/TransactionDetailPanel';
import { TransactionsFilters } from './transactions/TransactionsFilters';
import { TransactionsHeader } from './transactions/TransactionsHeader';
import { TransactionsPager } from './transactions/TransactionsPager';
import { TransactionsTable } from './transactions/TransactionsTable';
import type { TransactionEndpointType } from './transactions/types';

export default function TransactionsPage() {
  const [selectedTx, setSelectedTx] = useState<AdminTransaction | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [selectedIndex, setSelectedIndex] = useState<number>(-1);
  const [showBulkConfirm, setShowBulkConfirm] = useState<{ action: string; count: number } | null>(null);
  const [filterAsset, setFilterAsset] = useState('');
  const [filterId, setFilterId] = useState('');
  const [filterHash, setFilterHash] = useState('');
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [assetId, setAssetId] = useState('');
  const [amount, setAmount] = useState('');
  const [sourceType, setSourceType] = useState<TransactionEndpointType>('EXTERNAL_RANDOM');
  const [sourceAddress, setSourceAddress] = useState('');
  const [sourceVaultId, setSourceVaultId] = useState('');
  const [destinationType, setDestinationType] = useState<TransactionEndpointType>('VAULT');
  const [destinationAddress, setDestinationAddress] = useState('');
  const [destinationVaultId, setDestinationVaultId] = useState('');
  const [destinationTag, setDestinationTag] = useState('');
  const [hash, setHash] = useState('');
  const [feeLevel, setFeeLevel] = useState<FeeLevel>('MEDIUM');
  const [treatAsGrossAmount, setTreatAsGrossAmount] = useState(false);
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const { data: transactionsPage, isLoading, error } = useTransactionsPaged({
    pageIndex,
    pageSize,
    assetId: filterAsset || undefined,
    transactionId: filterId || undefined,
    hash: filterHash || undefined,
  });
  
  const { data: vaults } = useVaults();
  const { data: assets } = useAssets();
  const { data: adminAssets } = useAdminAssets();
  const { data: feeEstimates, isLoading: feeEstimatesLoading } = useEstimateFee(assetId || undefined);

  const transition = useTransitionTransaction();

  const createTransaction = useCreateTransaction();

  const { showToast } = useToast();
  const { email: currentUserEmail } = useCurrentUser();
  
  const resolveVaultAddress = (vaultId: string, asset: string) => {
    if (!vaultId || !asset) return '';
    const vault = (vaults || []).find((item) => item.id === vaultId);
    const wallet = vault?.wallets.find((item) => item.assetId === asset);
    return wallet?.depositAddress || '';
  };

  
  const ensureVaultAddress = async (vaultId: string, asset: string, label: string) => {
    if (!vaultId || !asset) return '';

    // Check if the vault exists in the current workspace before trying to create a wallet
    const vaultExists = (vaults || []).some((v) => v.id === vaultId);
    if (!vaultExists) return '';

    const resolved = resolveVaultAddress(vaultId, asset);
    if (resolved) return resolved;

    const response = await adminApi.createWallet(vaultId, { assetId: asset });
    if (response.error) {
      showToast({
        title: `${label} wallet could not be created`,
        description: response.error.message,
        type: 'error',
        duration: 5000,
      });
      return '';
    }

    const address = response.data?.depositAddress || '';
    if (!address) {
      showToast({
        title: `${label} address is unavailable`,
        description: `No deposit address returned for ${asset}.`,
        type: 'error',
        duration: 5000,
      });
    }

    return address;
  };

  const pagedTransactions = useMemo(() => transactionsPage?.items || [], [transactionsPage]);
  const totalCount = transactionsPage?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  useEffect(() => {
    if (pagedTransactions.length > 0 && selectedIndex >= pagedTransactions.length) {
      setSelectedIndex(pagedTransactions.length - 1);
    } else if (pagedTransactions.length === 0) {
      setSelectedIndex(-1);
    }
  }, [pagedTransactions, selectedIndex]);

  useEffect(() => {
    setPageIndex(0);
  }, [filterAsset, filterId, filterHash, pageSize]);

  useEffect(() => {
    if (pageIndex > totalPages - 1) {
      setPageIndex(Math.max(0, totalPages - 1));
    }
  }, [pageIndex, totalPages]);

  useEffect(() => {
    setSelectedIds(new Set());
    setSelectedIndex(-1);
  }, [pageIndex, filterAsset, filterId, filterHash, pageSize]);

  useEffect(() => {
    if (selectedTx && pagedTransactions.length > 0) {
      const updated = pagedTransactions.find(tx => tx.id === selectedTx.id);
      if (updated) {
        setSelectedTx(updated);
      }
    }
  }, [pagedTransactions, selectedTx]);

  useKeyboardShortcuts(
    [
      {
        key: 'j',
        handler: () => setSelectedIndex((prev) => Math.min(pagedTransactions.length - 1, prev + 1)),
        description: 'Move selection down',
      },
      {
        key: 'ArrowDown',
        handler: () => setSelectedIndex((prev) => Math.min(pagedTransactions.length - 1, prev + 1)),
        description: 'Move selection down',
      },
      {
        key: 'k',
        handler: () => setSelectedIndex((prev) => Math.max(0, prev - 1)),
        description: 'Move selection up',
      },
      {
        key: 'ArrowUp',
        handler: () => setSelectedIndex((prev) => Math.max(0, prev - 1)),
        description: 'Move selection up',
      },
      {
        key: 'Enter',
        handler: () => {
          if (selectedIndex >= 0 && pagedTransactions[selectedIndex]) {
            setSelectedTx(pagedTransactions[selectedIndex]);
          }
        },
        description: 'Open detail panel',
      },
      {
        key: ' ',
        handler: () => {
          if (selectedIndex >= 0 && pagedTransactions[selectedIndex]) {
            toggleSelection(pagedTransactions[selectedIndex].id);
          }
        },
        description: 'Toggle checkbox',
      },
      {
        key: 'a',
        ctrlKey: true,
        handler: () => {
          if (pagedTransactions.length > 0) {
            setSelectedIds(new Set(pagedTransactions.map((tx) => tx.id)));
          }
        },
        description: 'Select all',
      },
      {
        key: 'd',
        ctrlKey: true,
        handler: () => setSelectedIds(new Set()),
        description: 'Deselect all',
      },
      {
        key: 'Escape',
        handler: () => {
          setSelectedTx(null);
          setSelectedIds(new Set());
          setSelectedIndex(-1);
        },
        description: 'Close panel and clear selection',
      },
    ],
    !selectedTx
  );

  useKeyboardShortcuts(
    [
      {
        key: 'a',
        handler: () => {
          if (selectedTx && getAvailableActions(selectedTx.state).includes('approve')) {
            handleTransition(selectedTx.id, 'approve');
          }
        },
        description: 'Approve transaction',
      },
      {
        key: 's',
        handler: () => {
          if (selectedTx && getAvailableActions(selectedTx.state).includes('sign')) {
            handleTransition(selectedTx.id, 'sign');
          }
        },
        description: 'Sign transaction',
      },
      {
        key: 'c',
        handler: () => {
          if (selectedTx && getAvailableActions(selectedTx.state).includes('complete')) {
            handleTransition(selectedTx.id, 'complete');
          }
        },
        description: 'Complete transaction',
      },
      {
        key: 'f',
        handler: () => {
          if (selectedTx && getAvailableActions(selectedTx.state).includes('fail')) {
            const reason = prompt('Enter failure reason (INSUFFICIENT_FUNDS, NETWORK_ERROR, etc):');
            if (reason) {
              handleTransition(selectedTx.id, 'fail', reason);
            }
          }
        },
        description: 'Fail transaction',
      },
      {
        key: 'x',
        handler: () => {
          if (selectedTx && getAvailableActions(selectedTx.state).includes('cancel')) {
            if (confirm('Are you sure you want to cancel this transaction?')) {
              handleTransition(selectedTx.id, 'cancel');
            }
          }
        },
        description: 'Cancel transaction',
      },
      {
        key: 'c',
        ctrlKey: true,
        handler: () => {
          if (selectedTx) {
            navigator.clipboard.writeText(selectedTx.id);
            showToast({ title: 'Copied to clipboard', duration: 2000, type: 'success' });
          }
        },
        description: 'Copy transaction ID',
      },
      {
        key: 'Escape',
        handler: () => setSelectedTx(null),
        description: 'Close detail panel',
      },
    ],
    !!selectedTx
  );

  useEffect(() => {
    if (!vaults || vaults.length === 0) return;

    // Reset vault IDs if they no longer exist in the current workspace
    const sourceVaultExists = vaults.some((v) => v.id === sourceVaultId);
    const destVaultExists = vaults.some((v) => v.id === destinationVaultId);

    if (sourceType === 'VAULT' && (!sourceVaultId || !sourceVaultExists)) {
      setSourceVaultId(vaults[0].id);
    }
    if (destinationType === 'VAULT' && (!destinationVaultId || !destVaultExists)) {
      setDestinationVaultId(vaults[0].id);
    }
  }, [vaults, sourceType, destinationType, sourceVaultId, destinationVaultId]);

  useEffect(() => {
    if (!assets || assets.length === 0) return;
    if (!assetId) {
      setAssetId(assets[0].id);
    }
  }, [assets, assetId]);

  const selectedAdminAsset = (adminAssets || []).find((asset) => asset.id === assetId);
  const isMemoBased = selectedAdminAsset?.blockchainType === 'MemoBased';

  useEffect(() => {
    if (!isMemoBased && destinationTag) {
      setDestinationTag('');
    }
  }, [isMemoBased, destinationTag]);

  useEffect(() => {
    let canceled = false;

    const updateAddress = async () => {
      if (!assetId) return;

      if (sourceType === 'VAULT') {
        const resolved = await ensureVaultAddress(sourceVaultId, assetId, 'Source');
        if (!canceled) {
          setSourceAddress(resolved);
        }
      }
    };

    updateAddress();
    return () => {
      canceled = true;
    };
  }, [sourceType, sourceVaultId, assetId, vaults]);

  useEffect(() => {
    let canceled = false;

    const updateAddress = async () => {
      if (!assetId) return;

      if (destinationType === 'VAULT') {
        const resolved = await ensureVaultAddress(destinationVaultId, assetId, 'Destination');
        if (!canceled) {
          setDestinationAddress(resolved);
        }
      }
    };

    updateAddress();
    return () => {
      canceled = true;
    };
  }, [destinationType, destinationVaultId, assetId, vaults]);

  if (isLoading) return <div className="p-8 text-center text-muted">Loading transactions...</div>;
  if (error) return <div className="p-8 text-center text-red-500">Error: {error.message}</div>;

  const toggleSelection = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleTransition = async (id: string, action: string, reason?: string) => {
    try {
      const result = await transition.mutateAsync({ id, action, reason });
      if (result.error) {
        showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      } else {
        showToast({ title: `Transaction ${action}ed successfully`, type: 'success', duration: 3000 });
      }
    } catch (err) {
      showToast({ title: `Failed: ${err}`, type: 'error', duration: 5000 });
    }
  };

  const handleCreateTransaction = async () => {
    if (!assetId) {
      showToast({ title: 'Asset is required', type: 'error' });
      return;
    }
    if (!amount.trim()) {
      showToast({ title: 'Amount is required', type: 'error' });
      return;
    }

    const resolveVaultDefault = async (
      vaultId: string,
      update: (value: string) => void,
      label: string
    ) => {
      const resolved = await ensureVaultAddress(vaultId, assetId, label);
      if (resolved) {
        update(resolved);
      }
      return resolved;
    };

    const resolveOneTimeAddress = (current: string) => current.trim();

    const resolveSourceAddress = async () => {
      if (sourceType === 'VAULT') {
        return await resolveVaultDefault(sourceVaultId, setSourceAddress, 'Source');
      }
      if (sourceType === 'EXTERNAL_RANDOM') {
        return '';
      }
      return resolveOneTimeAddress(sourceAddress);
    };

    const resolvedSourceAddress = await resolveSourceAddress();
    const resolvedDestinationAddress = destinationType === 'VAULT'
      ? await resolveVaultDefault(destinationVaultId, setDestinationAddress, 'Destination')
      : resolveOneTimeAddress(destinationAddress);

    if (!resolvedSourceAddress && sourceType !== 'EXTERNAL_RANDOM') {
      showToast({ title: 'Source address is required', type: 'error' });
      return;
    }

    if (!resolvedDestinationAddress) {
      showToast({ title: 'Destination address is required', type: 'error' });
      return;
    }

    const result = await createTransaction.mutateAsync({
      assetId: assetId,
      amount: amount.trim(),
      sourceAddress: resolvedSourceAddress,
      destinationAddress: resolvedDestinationAddress,
      destinationTag: destinationTag.trim() || undefined,
      hash: hash.trim() || undefined,
      feeLevel: feeLevel,
      treatAsGrossAmount: treatAsGrossAmount,
      initiatedBy: currentUserEmail || undefined,
    });

    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
    } else {
      showToast({ title: 'Transaction created', type: 'success', duration: 3000 });
      setAssetId('');
      setAmount('');
      setSourceType('EXTERNAL_RANDOM');
      setSourceAddress('');
      setDestinationAddress('');
      setDestinationTag('');
      setHash('');
      setFeeLevel('MEDIUM');
      setTreatAsGrossAmount(false);
      setShowCreateForm(false);
    }
  };

  const handleBulkAction = async (action: string) => {
    const selectedTxs = pagedTransactions.filter((tx) => selectedIds.has(tx.id));
    const validTxs = selectedTxs.filter((tx) => getAvailableActions(tx.state).includes(action));

    if (validTxs.length === 0) {
      showToast({ title: 'No valid transactions for this action', type: 'error' });
      return;
    }

    setShowBulkConfirm({ action, count: validTxs.length });
  };

  const executeBulkAction = async () => {
    if (!showBulkConfirm) return;

    const { action } = showBulkConfirm;
    const selectedTxs = pagedTransactions.filter((tx) => selectedIds.has(tx.id));
    const validTxs = selectedTxs.filter((tx) => getAvailableActions(tx.state).includes(action));

    let successCount = 0;
    let failCount = 0;

    for (const tx of validTxs) {
      try {
        const result = await transition.mutateAsync({ id: tx.id, action });
        if (!result.error) {
          successCount++;
        } else {
          failCount++;
        }
      } catch {
        failCount++;
      }
    }

    showToast({
      title: `Bulk ${action} completed`,
      description: `${successCount} of ${validTxs.length} transactions ${action}ed successfully${failCount > 0 ? `, ${failCount} failed` : ''}`,
      type: failCount > 0 ? 'error' : 'success',
      duration: 5000,
    });

    setSelectedIds(new Set());
    setShowBulkConfirm(null);
  };

  const getAvailableActions = (state: string) => {
    const actions: string[] = [];

    switch (state) {
      case 'SUBMITTED':
        actions.push('approve', 'reject', 'cancel');
        break;
      case 'PENDING_SIGNATURE':
        actions.push('approve', 'reject', 'cancel');
        break;
      case 'PENDING_AUTHORIZATION':
        actions.push('sign', 'reject', 'cancel');
        break;
      case 'QUEUED':
        actions.push('broadcast', 'cancel');
        break;
      case 'BROADCASTING':
        actions.push('confirm', 'fail', 'timeout');
        break;
      case 'CONFIRMING':
        actions.push('complete', 'fail', 'timeout');
        break;
    }

    return actions;
  };

  return (
    <div>
      <TransactionsHeader
        totalCount={totalCount}
        showCreateForm={showCreateForm}
        onToggleCreate={() => setShowCreateForm((prev) => !prev)}
        selectedCount={selectedIds.size}
        onBulkAction={handleBulkAction}
        onClearSelection={() => setSelectedIds(new Set())}
      />

      {showCreateForm && (
        <CreateTransactionForm
          assets={assets || []}
          vaults={vaults || []}
          assetId={assetId}
          setAssetId={setAssetId}
          sourceType={sourceType}
          setSourceType={(type) => {
            setSourceType(type);
            if (type === 'VAULT' || type === 'EXTERNAL_RANDOM') {
              setSourceAddress('');
            }
          }}
          sourceAddress={sourceAddress}
          setSourceAddress={setSourceAddress}
          sourceVaultId={sourceVaultId}
          setSourceVaultId={setSourceVaultId}
          destinationType={destinationType}
          setDestinationType={(type) => {
            setDestinationType(type);
            if (type === 'VAULT') {
              setDestinationAddress('');
            }
          }}
          destinationAddress={destinationAddress}
          setDestinationAddress={setDestinationAddress}
          destinationVaultId={destinationVaultId}
          setDestinationVaultId={setDestinationVaultId}
          destinationTag={destinationTag}
          setDestinationTag={setDestinationTag}
          amount={amount}
          setAmount={setAmount}
          hash={hash}
          setHash={setHash}
          feeLevel={feeLevel}
          setFeeLevel={setFeeLevel}
          treatAsGrossAmount={treatAsGrossAmount}
          setTreatAsGrossAmount={setTreatAsGrossAmount}
          feeEstimates={feeEstimates}
          feeEstimatesLoading={feeEstimatesLoading}
          isMemoBased={isMemoBased}
          onSubmit={handleCreateTransaction}
          onCancel={() => setShowCreateForm(false)}
          isSubmitting={createTransaction.isPending}
        />
      )}

      <TransactionsFilters
        assets={assets || []}
        filterAsset={filterAsset}
        setFilterAsset={setFilterAsset}
        filterId={filterId}
        setFilterId={setFilterId}
        filterHash={filterHash}
        setFilterHash={setFilterHash}
        onClear={() => {
          setFilterAsset('');
          setFilterId('');
          setFilterHash('');
        }}
      />

      <TransactionsPager
        pageIndex={pageIndex}
        totalPages={totalPages}
        setPageIndex={setPageIndex}
        pageSize={pageSize}
        setPageSize={setPageSize}
      />

      <TransactionsTable
        transactions={pagedTransactions}
        selectedIds={selectedIds}
        selectedIndex={selectedIndex}
        onSelect={setSelectedTx}
        onToggleSelection={toggleSelection}
        onToggleAll={(checked) => {
          if (checked && pagedTransactions.length > 0) {
            setSelectedIds(new Set(pagedTransactions.map((tx) => tx.id)));
          } else {
            setSelectedIds(new Set());
          }
        }}
      />

      {showBulkConfirm && (
        <BulkConfirmDialog
          action={showBulkConfirm.action}
          count={showBulkConfirm.count}
          onCancel={() => setShowBulkConfirm(null)}
          onConfirm={executeBulkAction}
        />
      )}

      {selectedTx && (
        <TransactionDetailPanel
          transaction={selectedTx}
          onClose={() => setSelectedTx(null)}
          getAvailableActions={getAvailableActions}
          onTransition={handleTransition}
        />
      )}
    </div>
  );
}









