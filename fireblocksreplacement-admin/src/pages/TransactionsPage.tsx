import { useState, useEffect, useMemo } from 'react';
import { useTransactions, useTransitionTransaction } from '../api/queries';
import { useToast } from '../components/ToastProvider';
import { useKeyboardShortcuts } from '../hooks/useKeyboardShortcuts';
import type { AdminTransaction } from '../types/admin';

export default function TransactionsPage() {
  const { data: transactions, isLoading, error } = useTransactions();
  const transition = useTransitionTransaction();
  const { showToast } = useToast();
  const [selectedTx, setSelectedTx] = useState<AdminTransaction | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [selectedIndex, setSelectedIndex] = useState<number>(-1);
  const [showBulkConfirm, setShowBulkConfirm] = useState<{ action: string; count: number } | null>(null);
  const [filterAsset, setFilterAsset] = useState('');
  const [filterId, setFilterId] = useState('');
  const [filterHash, setFilterHash] = useState('');

  const displayedTransactions = useMemo(() => {
    const normalizedAsset = filterAsset.trim().toLowerCase();
    const normalizedId = filterId.trim().toLowerCase();
    const normalizedHash = filterHash.trim().toLowerCase();

    const filtered = (transactions || []).filter((tx) => {
      const assetMatch = !normalizedAsset || tx.assetId.toLowerCase().includes(normalizedAsset);
      const idMatch = !normalizedId || tx.id.toLowerCase().includes(normalizedId);
      const hashMatch = !normalizedHash || (tx.hash || '').toLowerCase().includes(normalizedHash);
      return assetMatch && idMatch && hashMatch;
    });

    return filtered
      .slice()
      .sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));
  }, [transactions, filterAsset, filterId, filterHash]);

  // Reset selection when transactions change
  useEffect(() => {
    if (displayedTransactions.length > 0 && selectedIndex >= displayedTransactions.length) {
      setSelectedIndex(displayedTransactions.length - 1);
    } else if (displayedTransactions.length === 0) {
      setSelectedIndex(-1);
    }
  }, [displayedTransactions, selectedIndex]);

  // Keyboard shortcuts for list navigation
  useKeyboardShortcuts(
    [
      {
        key: 'j',
        handler: () => setSelectedIndex((prev) => Math.min(displayedTransactions.length - 1, prev + 1)),
        description: 'Move selection down',
      },
      {
        key: 'ArrowDown',
        handler: () => setSelectedIndex((prev) => Math.min(displayedTransactions.length - 1, prev + 1)),
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
          if (selectedIndex >= 0 && displayedTransactions[selectedIndex]) {
            setSelectedTx(displayedTransactions[selectedIndex]);
          }
        },
        description: 'Open detail panel',
      },
      {
        key: ' ',
        handler: () => {
          if (selectedIndex >= 0 && displayedTransactions[selectedIndex]) {
            toggleSelection(displayedTransactions[selectedIndex].id);
          }
        },
        description: 'Toggle checkbox',
      },
      {
        key: 'a',
        ctrlKey: true,
        handler: () => {
          if (displayedTransactions.length > 0) {
            setSelectedIds(new Set(displayedTransactions.map((tx) => tx.id)));
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

  if (isLoading) return <div>Loading transactions...</div>;
  if (error) return <div>Error: {error.message}</div>;

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

  const handleBulkAction = async (action: string) => {
    const selectedTxs = transactions?.filter((tx) => selectedIds.has(tx.id)) || [];
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
    const selectedTxs = transactions?.filter((tx) => selectedIds.has(tx.id)) || [];
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
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
        <h2>
          Transactions ({displayedTransactions.length}
          {transactions && displayedTransactions.length !== transactions.length ? ` of ${transactions.length}` : ''})
        </h2>
        {selectedIds.size > 0 && (
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
            <span style={{ color: '#aaa', fontSize: '0.875rem' }}>
              {selectedIds.size} selected
            </span>
            <button className="btn btn-primary" onClick={() => handleBulkAction('approve')}>
              Approve All
            </button>
            <button className="btn btn-primary" onClick={() => handleBulkAction('sign')}>
              Sign All
            </button>
            <button className="btn btn-primary" onClick={() => handleBulkAction('complete')}>
              Complete All
            </button>
            <button className="btn btn-danger" onClick={() => setSelectedIds(new Set())}>
              Clear Selection
            </button>
          </div>
        )}
      </div>

      <div style={{ display: 'grid', gap: '0.5rem', gridTemplateColumns: '1fr 1fr 1fr auto', marginBottom: '1rem' }}>
        <input
          type="text"
          placeholder="Filter by asset"
          value={filterAsset}
          onChange={(e) => setFilterAsset(e.target.value)}
          style={{
            padding: '0.5rem',
            background: '#1a1a1a',
            border: '1px solid #444',
            color: '#fff',
            borderRadius: '4px',
          }}
        />
        <input
          type="text"
          placeholder="Filter by transaction ID"
          value={filterId}
          onChange={(e) => setFilterId(e.target.value)}
          style={{
            padding: '0.5rem',
            background: '#1a1a1a',
            border: '1px solid #444',
            color: '#fff',
            borderRadius: '4px',
          }}
        />
        <input
          type="text"
          placeholder="Filter by transaction hash"
          value={filterHash}
          onChange={(e) => setFilterHash(e.target.value)}
          style={{
            padding: '0.5rem',
            background: '#1a1a1a',
            border: '1px solid #444',
            color: '#fff',
            borderRadius: '4px',
          }}
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
          Clear Filters
        </button>
      </div>

      <table>
        <thead>
          <tr>
            <th style={{ width: '40px' }}>
              <input
                type="checkbox"
                checked={selectedIds.size === displayedTransactions.length && displayedTransactions.length > 0}
                onChange={(e) => {
                  if (e.target.checked && displayedTransactions.length > 0) {
                    setSelectedIds(new Set(displayedTransactions.map((tx) => tx.id)));
                  } else {
                    setSelectedIds(new Set());
                  }
                }}
                style={{ cursor: 'pointer' }}
              />
            </th>
            <th>ID</th>
            <th>State</th>
            <th>Asset</th>
            <th>Amount</th>
            <th>Vault</th>
            <th>Destination</th>
            <th>Created</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {displayedTransactions.map((tx, index) => (
            <tr
              key={tx.id}
              onClick={() => setSelectedTx(tx)}
              style={{
                cursor: 'pointer',
                background: selectedIndex === index ? '#2a2a2a' : undefined,
              }}
            >
              <td onClick={(e) => e.stopPropagation()}>
                <input
                  type="checkbox"
                  checked={selectedIds.has(tx.id)}
                  onChange={() => toggleSelection(tx.id)}
                  style={{ cursor: 'pointer' }}
                />
              </td>
              <td style={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
                {tx.id.substring(0, 8)}...
              </td>
              <td>
                <span className={`state-badge state-${tx.state}`}>
                  {tx.state}
                </span>
              </td>
              <td>{tx.assetId}</td>
              <td>{parseFloat(tx.amount).toFixed(4)}</td>
              <td style={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
                {tx.vaultAccountId.substring(0, 8)}...
              </td>
              <td style={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
                {tx.destinationAddress.substring(0, 12)}...
              </td>
              <td>{new Date(tx.createdAt).toLocaleString()}</td>
              <td>
                <button
                  className="btn btn-primary"
                  onClick={(e) => {
                    e.stopPropagation();
                    setSelectedTx(tx);
                  }}
                  style={{ fontSize: '0.75rem', padding: '0.25rem 0.5rem' }}
                >
                  View
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {/* Bulk confirmation dialog */}
      {showBulkConfirm && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0, 0, 0, 0.7)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9998,
          }}
          onClick={() => setShowBulkConfirm(null)}
        >
          <div
            style={{
              background: '#1a1a1a',
              border: '1px solid #333',
              borderRadius: '8px',
              padding: '2rem',
              maxWidth: '500px',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <h3>Confirm Bulk Action</h3>
            <p>
              Are you sure you want to {showBulkConfirm.action} {showBulkConfirm.count} transaction(s)?
            </p>
            <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
              <button className="btn btn-primary" onClick={() => setShowBulkConfirm(null)}>
                Cancel
              </button>
              <button className="btn btn-danger" onClick={executeBulkAction}>
                Confirm
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

          <div style={{ marginBottom: '2rem' }}>
            <h3>Information</h3>
            <div style={{ display: 'grid', gap: '0.5rem' }}>
              <div><strong>ID:</strong> <span style={{ fontFamily: 'monospace' }}>{selectedTx.id}</span></div>
              <div><strong>State:</strong> <span className={`state-badge state-${selectedTx.state}`}>{selectedTx.state}</span></div>
              <div><strong>Asset:</strong> {selectedTx.assetId}</div>
              <div><strong>Amount:</strong> {selectedTx.amount}</div>
              <div><strong>Vault:</strong> <span style={{ fontFamily: 'monospace' }}>{selectedTx.vaultAccountId}</span></div>
              <div><strong>Destination:</strong> <span style={{ fontFamily: 'monospace' }}>{selectedTx.destinationAddress}</span></div>
              {selectedTx.hash && <div><strong>Hash:</strong> <span style={{ fontFamily: 'monospace' }}>{selectedTx.hash}</span></div>}
              <div><strong>Fee:</strong> {selectedTx.fee}</div>
              <div><strong>Network Fee:</strong> {selectedTx.networkFee}</div>
              <div><strong>Confirmations:</strong> {selectedTx.confirmations}</div>
              {selectedTx.failureReason && <div><strong>Failure Reason:</strong> {selectedTx.failureReason}</div>}
              <div><strong>Created:</strong> {new Date(selectedTx.createdAt).toLocaleString()}</div>
              <div><strong>Updated:</strong> {new Date(selectedTx.updatedAt).toLocaleString()}</div>
            </div>
          </div>

          <div>
            <h3>State Transitions</h3>
            {getAvailableActions(selectedTx.state).length > 0 ? (
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                {getAvailableActions(selectedTx.state).map((action) => (
                  <button
                    key={action}
                    className={`btn ${action === 'fail' || action === 'reject' || action === 'cancel' || action === 'timeout' ? 'btn-danger' : 'btn-primary'}`}
                    onClick={() => {
                      if (action === 'fail') {
                        const reason = prompt('Enter failure reason (INSUFFICIENT_FUNDS, NETWORK_ERROR, etc):');
                        if (reason) {
                          handleTransition(selectedTx.id, action, reason);
                        }
                      } else if (action === 'reject' || action === 'cancel' || action === 'timeout') {
                        if (confirm(`Are you sure you want to ${action} this transaction?`)) {
                          handleTransition(selectedTx.id, action);
                        }
                      } else {
                        handleTransition(selectedTx.id, action);
                      }
                    }}
                    disabled={transition.isPending}
                    title={`Keyboard shortcut: ${action[0]}`}
                  >
                    {action.charAt(0).toUpperCase() + action.slice(1)}
                  </button>
                ))}
              </div>
            ) : (
              <p>No transitions available (terminal state)</p>
            )}
          </div>

          <div style={{ marginTop: '2rem' }}>
            <details>
              <summary style={{ cursor: 'pointer', fontWeight: 'bold' }}>Raw JSON</summary>
              <pre style={{ background: '#000', padding: '1rem', borderRadius: '4px', overflow: 'auto', fontSize: '0.75rem' }}>
                {JSON.stringify(selectedTx, null, 2)}
              </pre>
            </details>
          </div>
        </div>
      )}
    </div>
  );
}
