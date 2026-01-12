import { useState, useEffect, useMemo } from 'react';
import { useTransactionsPaged, useTransitionTransaction, useCreateTransaction, useVaults, useAssets } from '../api/queries';
import { useToast } from '../components/ToastProvider';
import { useKeyboardShortcuts } from '../hooks/useKeyboardShortcuts';
import type { AdminTransaction } from '../types/admin';

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
  const [sourceType, setSourceType] = useState<'EXTERNAL' | 'INTERNAL'>('EXTERNAL');
  const [sourceAddress, setSourceAddress] = useState('');
  const [sourceVaultId, setSourceVaultId] = useState('');
  const [destinationType, setDestinationType] = useState<'EXTERNAL' | 'INTERNAL'>('INTERNAL');
  const [destinationAddress, setDestinationAddress] = useState('');
  const [destinationVaultId, setDestinationVaultId] = useState('');
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
  const transition = useTransitionTransaction();
  const createTransaction = useCreateTransaction();
  const { showToast } = useToast();

  const getRandomHex = (length: number) => {
    const bytes = new Uint8Array(Math.ceil(length / 2));
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('').slice(0, length);
  };

  const generateExternalAddress = (asset: string) => {
    const normalized = asset.trim().toUpperCase();
    if (normalized === 'BTC') {
      return `bc1q${getRandomHex(38)}`;
    }
    if (normalized === 'ETH' || normalized === 'USDT' || normalized === 'USDC') {
      return `0x${getRandomHex(40)}`;
    }
    const prefix = asset.trim() ? asset.trim().toLowerCase() : 'asset';
    return `${prefix}_${getRandomHex(32)}`;
  };

  const formatVaultLabel = (id?: string, name?: string) => {
    if (!id) return '—';
    if (name) return `${name} (${id.slice(0, 8)}...)`;
    return `${id.slice(0, 8)}...`;
  };

  const pagedTransactions = useMemo(() => transactionsPage?.items || [], [transactionsPage]);
  const totalCount = transactionsPage?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Reset selection when transactions change
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

  // Sync selectedTx with latest data from transactions array (for WebSocket updates)
  useEffect(() => {
    if (selectedTx && pagedTransactions.length > 0) {
      const updated = pagedTransactions.find(tx => tx.id === selectedTx.id);
      if (updated) {
        setSelectedTx(updated);
      }
    }
  }, [pagedTransactions, selectedTx]);

  // Keyboard shortcuts for list navigation
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

  // Keyboard shortcuts for detail panel
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
            if (confirm(`Are you sure you want to cancel this transaction?`)) {
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
    if (sourceType === 'INTERNAL' && !sourceVaultId) {
      setSourceVaultId(vaults[0].id);
    }
    if (destinationType === 'INTERNAL' && !destinationVaultId) {
      setDestinationVaultId(vaults[0].id);
    }
  }, [vaults, sourceType, destinationType, sourceVaultId, destinationVaultId]);

  useEffect(() => {
    if (!assets || assets.length === 0) return;
    if (!assetId) {
      setAssetId(assets[0].id);
    }
  }, [assets, assetId]);

  useEffect(() => {
    if (sourceType === 'EXTERNAL' && assetId && !sourceAddress.trim()) {
      setSourceAddress(generateExternalAddress(assetId));
    }
  }, [sourceType, assetId, sourceAddress]);

  useEffect(() => {
    if (destinationType === 'EXTERNAL' && assetId && !destinationAddress.trim()) {
      setDestinationAddress(generateExternalAddress(assetId));
    }
  }, [destinationType, assetId, destinationAddress]);

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

    if (sourceType === 'INTERNAL' && !sourceVaultId) {
      showToast({ title: 'Source vault is required', type: 'error' });
      return;
    }
    if (destinationType === 'INTERNAL' && !destinationVaultId) {
      showToast({ title: 'Destination vault is required', type: 'error' });
      return;
    }

    const resolvedSourceAddress = sourceType === 'EXTERNAL'
      ? (sourceAddress.trim() || generateExternalAddress(assetId))
      : undefined;
    const resolvedDestinationAddress = destinationType === 'EXTERNAL'
      ? (destinationAddress.trim() || generateExternalAddress(assetId))
      : undefined;

    if (sourceType === 'EXTERNAL' && resolvedSourceAddress && sourceAddress.trim() !== resolvedSourceAddress) {
      setSourceAddress(resolvedSourceAddress);
    }
    if (destinationType === 'EXTERNAL' && resolvedDestinationAddress && destinationAddress.trim() !== resolvedDestinationAddress) {
      setDestinationAddress(resolvedDestinationAddress);
    }

    const result = await createTransaction.mutateAsync({
      assetId: assetId,
      amount: amount.trim(),
      sourceType,
      sourceAddress: resolvedSourceAddress,
      sourceVaultAccountId: sourceType === 'INTERNAL' ? sourceVaultId : undefined,
      destinationType,
      destinationAddress: resolvedDestinationAddress,
      destinationVaultAccountId: destinationType === 'INTERNAL' ? destinationVaultId : undefined,
    });

    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
    } else {
      showToast({ title: 'Transaction created', type: 'success', duration: 3000 });
      setAssetId('');
      setAmount('');
      setSourceAddress('');
      setDestinationAddress('');
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
      <div className="flex-between mb-4">
        <h2>
          Transactions <span className="text-muted text-sm">({totalCount})</span>
        </h2>
        <div className="flex-gap-4">
          <button
            className="btn btn-primary"
            onClick={() => setShowCreateForm((prev) => !prev)}
          >
            {showCreateForm ? 'Close' : '+ New Transaction'}
          </button>

          {selectedIds.size > 0 && (
            <div className="flex-gap-2 items-center bg-secondary p-2 rounded-md">
              <span className="text-muted text-sm px-2">
                {selectedIds.size} selected
              </span>
              <button className="btn btn-primary" onClick={() => handleBulkAction('approve')}>
                Approve
              </button>
              <button className="btn btn-primary" onClick={() => handleBulkAction('sign')}>
                Sign
              </button>
              <button className="btn btn-primary" onClick={() => handleBulkAction('complete')}>
                Complete
              </button>
              <button className="btn btn-danger" onClick={() => setSelectedIds(new Set())}>
                Clear
              </button>
            </div>
          )}
        </div>
      </div>

      {showCreateForm && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleCreateTransaction();
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
                {(assets || []).map((asset) => (
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
                    onChange={(e) => setSourceType(e.target.value as 'EXTERNAL' | 'INTERNAL')}
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
                      {(vaults || []).map((vault) => (
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
                    onChange={(e) => setDestinationType(e.target.value as 'EXTERNAL' | 'INTERNAL')}
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
                      {(vaults || []).map((vault) => (
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
              onClick={() => setShowCreateForm(false)}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={createTransaction.isPending}
            >
              Create Transaction
            </button>
          </div>
        </form>
      )}

      <div className="flex gap-4 mb-6">
        <select
          value={filterAsset}
          onChange={(e) => setFilterAsset(e.target.value)}
          className="flex-1"
        >
          <option value="">All assets</option>
          {(assets || []).map((asset) => (
            <option key={asset.id} value={asset.id}>
              {asset.name} ({asset.symbol})
            </option>
          ))}
        </select>
        <input
          type="text"
          placeholder="Filter by ID"
          value={filterId}
          onChange={(e) => setFilterId(e.target.value)}
          className="flex-1"
        />
        <input
          type="text"
          placeholder="Filter by hash"
          value={filterHash}
          onChange={(e) => setFilterHash(e.target.value)}
          className="flex-1"
        />
        <button
          className="btn btn-secondary"
          onClick={() => {
            setFilterAsset('');
            setFilterId('');
            setFilterHash('');
          }}
          disabled={!filterAsset && !filterId && !filterHash}
        >
          Clear
        </button>
      </div>

      <div className="flex-between mb-3">
        <div className="flex-gap-2 items-center">
          <button
            className="btn btn-secondary text-sm py-1 px-2"
            onClick={() => setPageIndex((prev) => Math.max(0, prev - 1))}
            disabled={pageIndex === 0}
          >
            Previous
          </button>
          <span className="text-muted text-sm">
            Page {pageIndex + 1} of {totalPages}
          </span>
          <button
            className="btn btn-secondary text-sm py-1 px-2"
            onClick={() => setPageIndex((prev) => Math.min(totalPages - 1, prev + 1))}
            disabled={pageIndex >= totalPages - 1}
          >
            Next
          </button>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-muted text-sm">Rows per page</span>
          <select
            value={pageSize}
            onChange={(e) => setPageSize(Number(e.target.value))}
            className="py-1 px-2 text-sm w-auto"
          >
            {[10, 25, 50, 100].map((size) => (
              <option key={size} value={size}>{size}</option>
            ))}
          </select>
        </div>
      </div>

      <div className="overflow-x-auto rounded-lg border border-tertiary">
        <table className="w-full">
          <thead>
            <tr>
              <th className="w-10 text-center">
                <input
                  type="checkbox"
                  checked={selectedIds.size === pagedTransactions.length && pagedTransactions.length > 0}
                  onChange={(e) => {
                    if (e.target.checked && pagedTransactions.length > 0) {
                      setSelectedIds(new Set(pagedTransactions.map((tx) => tx.id)));
                    } else {
                      setSelectedIds(new Set());
                    }
                  }}
                  className="cursor-pointer"
                />
              </th>
              <th>ID</th>
              <th>State</th>
              <th>Asset</th>
              <th>Amount</th>
              <th>Source</th>
              <th>Destination</th>
              <th>Created</th>
              <th className="text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {pagedTransactions.map((tx, index) => (
              <tr
                key={tx.id}
                onClick={() => setSelectedTx(tx)}
                className={`cursor-pointer transition-colors ${selectedIndex === index ? 'bg-white/5' : 'hover:bg-white/5'}`}
                style={selectedIndex === index ? { backgroundColor: 'var(--bg-tertiary)' } : undefined}
              >
                <td className="text-center" onClick={(e) => e.stopPropagation()}>
                  <input
                    type="checkbox"
                    checked={selectedIds.has(tx.id)}
                    onChange={() => toggleSelection(tx.id)}
                    className="cursor-pointer"
                  />
                </td>
                <td className="text-mono text-sm text-muted">
                  {tx.id.substring(0, 8)}...
                </td>
                <td>
                  <span className={`state-badge state-${tx.state}`}>
                    {tx.state}
                  </span>
                </td>
                <td className="font-medium">{tx.assetId}</td>
                <td className="text-mono">{parseFloat(tx.amount).toFixed(4)}</td>
                <td className="text-mono text-sm text-muted">
                  {formatVaultLabel(tx.vaultAccountId)}
                </td>
                <td className="text-mono text-sm text-muted">
                  {tx.destinationType === 'INTERNAL'
                    ? formatVaultLabel(tx.destinationVaultAccountId, tx.destinationVaultAccountName)
                    : `${tx.destinationAddress.substring(0, 12)}...`}
                </td>
                <td className="text-sm text-muted">{new Date(tx.createdAt).toLocaleString()}</td>
                <td className="text-right">
                  <button
                    className="btn btn-ghost text-sm py-1 px-3"
                    onClick={(e) => {
                      e.stopPropagation();
                      setSelectedTx(tx);
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

      {/* Bulk confirmation dialog */}
      {showBulkConfirm && (
        <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 backdrop-blur-sm">
          <div className="bg-secondary border border-tertiary rounded-lg p-6 max-w-md w-full shadow-2xl animate-in fade-in zoom-in duration-200">
            <h3 className="text-lg font-bold mb-2">Confirm Bulk Action</h3>
            <p className="text-muted mb-6">
              Are you sure you want to <strong>{showBulkConfirm.action}</strong> {showBulkConfirm.count} transaction(s)?
            </p>
            <div className="flex justify-end gap-3">
              <button className="btn btn-secondary" onClick={() => setShowBulkConfirm(null)}>
                Cancel
              </button>
              <button className="btn btn-danger" onClick={executeBulkAction}>
                Confirm {showBulkConfirm.action}
              </button>
            </div>
          </div>
        </div>
      )}

      {selectedTx && (
        <div className="detail-panel">
          <div className="detail-panel-header">
            <h2>Transaction Details</h2>
            <button className="close-btn" onClick={() => setSelectedTx(null)}>×</button>
          </div>

          <div className="mb-8">
            <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Information</h3>
            <div className="grid gap-3 p-4 bg-tertiary/20 rounded-lg border border-tertiary">
              <div className="flex justify-between">
                <span className="text-muted">ID</span>
                <span className="text-mono select-all">{selectedTx.id}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-muted">State</span>
                <span className={`state-badge state-${selectedTx.state}`}>{selectedTx.state}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted">Asset</span>
                <span className="font-medium">{selectedTx.assetId}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted">Amount</span>
                <span className="text-mono">{selectedTx.amount}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted">Created</span>
                <span>{new Date(selectedTx.createdAt).toLocaleString()}</span>
              </div>
            </div>
          </div>

          <div className="mb-8">
            <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Flow</h3>
            <div className="grid gap-4">
              <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
                <div className="text-xs text-muted uppercase mb-1">Source</div>
                <div className="font-medium">{selectedTx.sourceType}</div>
                <div className="text-mono text-sm text-muted break-all mt-1">
                  {selectedTx.sourceType === 'INTERNAL'
                    ? `Vault: ${selectedTx.vaultAccountId}`
                    : selectedTx.sourceAddress}
                </div>
              </div>

              <div className="flex justify-center text-muted">↓</div>

              <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
                <div className="text-xs text-muted uppercase mb-1">Destination</div>
                <div className="font-medium">{selectedTx.destinationType}</div>
                <div className="text-mono text-sm text-muted break-all mt-1">
                  {selectedTx.destinationType === 'INTERNAL'
                    ? `Vault: ${selectedTx.destinationVaultAccountId}`
                    : selectedTx.destinationAddress}
                </div>
              </div>
            </div>
          </div>

          {selectedTx.hash && (
            <div className="mb-8">
              <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Blockchain</h3>
              <div className="p-4 bg-tertiary/20 rounded-lg border border-tertiary">
                <div className="text-xs text-muted uppercase mb-1">Transaction Hash</div>
                <div className="text-mono text-sm break-all text-accent cursor-pointer hover:underline">
                  {selectedTx.hash}
                </div>
              </div>
            </div>
          )}

          <div>
            <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Actions</h3>
            <div className="flex flex-wrap gap-2">
              {getAvailableActions(selectedTx.state).map((action) => (
                <button
                  key={action}
                  className={`btn ${action === 'fail' || action === 'reject' || action === 'cancel' || action === 'timeout'
                    ? 'btn-danger'
                    : 'btn-primary'
                    }`}
                  onClick={() => {
                    if (action === 'fail') {
                      const reason = prompt('Enter failure reason:');
                      if (reason) handleTransition(selectedTx.id, 'fail', reason);
                    } else if (action === 'cancel') {
                      if (confirm('Are you sure?')) handleTransition(selectedTx.id, 'cancel');
                    } else {
                      handleTransition(selectedTx.id, action);
                    }
                  }}
                >
                  {action.charAt(0).toUpperCase() + action.slice(1)}
                </button>
              ))}
              {getAvailableActions(selectedTx.state).length === 0 && (
                <div className="text-muted text-sm italic">No actions available for this state</div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
