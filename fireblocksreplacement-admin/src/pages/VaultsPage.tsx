import { useState } from 'react';
import { useVaults, useCreateVault, useFrozenBalances } from '../api/queries';
import type { AdminVault } from '../types/admin';

export default function VaultsPage() {
  const { data: vaults, isLoading, error } = useVaults();
  const createVault = useCreateVault();
  const [selectedVault, setSelectedVault] = useState<AdminVault | null>(null);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [vaultName, setVaultName] = useState('');
  const frozenBalancesQuery = useFrozenBalances(selectedVault?.id ?? '');

  if (isLoading) return <div>Loading vaults...</div>;
  if (error) return <div>Error: {error.message}</div>;

  const handleCreateVault = async () => {
    if (!vaultName.trim()) {
      alert('Vault name is required');
      return;
    }

    const result = await createVault.mutateAsync({ name: vaultName });
    if (result.error) {
      alert(`Error: ${result.error.message}`);
    } else {
      alert('Vault created successfully');
      setVaultName('');
      setShowCreateForm(false);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
        <h2>Vaults ({vaults?.length || 0})</h2>
        <button
          className="btn btn-primary"
          onClick={() => setShowCreateForm(!showCreateForm)}
        >
          {showCreateForm ? 'Cancel' : '+ Create Vault'}
        </button>
      </div>

      {showCreateForm && (
        <div style={{ background: '#252525', padding: '1rem', borderRadius: '8px', marginBottom: '1rem' }}>
          <h3>Create New Vault</h3>
          <input
            type="text"
            placeholder="Vault name"
            value={vaultName}
            onChange={(e) => setVaultName(e.target.value)}
            style={{
              width: '100%',
              padding: '0.5rem',
              marginBottom: '0.5rem',
              background: '#1a1a1a',
              border: '1px solid #444',
              color: '#fff',
              borderRadius: '4px'
            }}
          />
          <button
            className="btn btn-primary"
            onClick={handleCreateVault}
            disabled={createVault.isPending}
          >
            Create
          </button>
        </div>
      )}

      <table>
        <thead>
          <tr>
            <th>ID</th>
            <th>Name</th>
            <th>Assets</th>
            <th>Hidden</th>
            <th>Created</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {vaults?.map((vault) => (
            <tr key={vault.id} onClick={() => setSelectedVault(vault)} style={{ cursor: 'pointer' }}>
              <td style={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
                {vault.id.substring(0, 8)}...
              </td>
              <td>{vault.name}</td>
              <td>{vault.wallets.length}</td>
              <td>{vault.hiddenOnUI ? 'Yes' : 'No'}</td>
              <td>{new Date(vault.createdAt).toLocaleString()}</td>
              <td>
                <button
                  className="btn btn-primary"
                  onClick={(e) => {
                    e.stopPropagation();
                    setSelectedVault(vault);
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

      {selectedVault && (
        <div className="detail-panel">
          <div className="detail-panel-header">
            <h2>Vault Details</h2>
            <button className="close-btn" onClick={() => setSelectedVault(null)}>×</button>
          </div>

          <div style={{ marginBottom: '2rem' }}>
            <h3>Information</h3>
            <div style={{ display: 'grid', gap: '0.5rem' }}>
              <div><strong>ID:</strong> <span style={{ fontFamily: 'monospace' }}>{selectedVault.id}</span></div>
              <div><strong>Name:</strong> {selectedVault.name}</div>
              <div><strong>Hidden:</strong> {selectedVault.hiddenOnUI ? 'Yes' : 'No'}</div>
              <div><strong>Auto Fuel:</strong> {selectedVault.autoFuel ? 'Yes' : 'No'}</div>
              {selectedVault.customerRefId && <div><strong>Customer Ref:</strong> {selectedVault.customerRefId}</div>}
              <div><strong>Created:</strong> {new Date(selectedVault.createdAt).toLocaleString()}</div>
              <div><strong>Updated:</strong> {new Date(selectedVault.updatedAt).toLocaleString()}</div>
            </div>
          </div>

          <div style={{ marginBottom: '2rem' }}>
            <h3>Assets ({selectedVault.wallets.length})</h3>
            {selectedVault.wallets.length > 0 ? (
              <table style={{ fontSize: '0.875rem' }}>
                <thead>
                  <tr>
                    <th>Asset</th>
                    <th>Balance</th>
                    <th>Locked</th>
                    <th>Available</th>
                    <th>Addresses</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedVault.wallets.map((wallet) => (
                    <tr key={wallet.assetId}>
                      <td>{wallet.assetId}</td>
                      <td>{parseFloat(wallet.balance).toFixed(8)}</td>
                      <td style={{
                        color: parseFloat(wallet.lockedAmount) > 0 ? '#fbbf24' : 'inherit',
                        fontWeight: parseFloat(wallet.lockedAmount) > 0 ? 'bold' : 'normal'
                      }}>
                        {parseFloat(wallet.lockedAmount).toFixed(8)}
                      </td>
                      <td>{parseFloat(wallet.available).toFixed(8)}</td>
                      <td>{wallet.addressCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p>No assets in this vault</p>
            )}
          </div>

          <div style={{ marginBottom: '2rem' }}>
            <h3>Frozen Balances</h3>
            {frozenBalancesQuery.isLoading && <p>Loading frozen balances...</p>}
            {frozenBalancesQuery.error && (
              <p>Error loading frozen balances: {String(frozenBalancesQuery.error)}</p>
            )}
            {!frozenBalancesQuery.isLoading && !frozenBalancesQuery.error && (
              frozenBalancesQuery.data && frozenBalancesQuery.data.length > 0 ? (
                <table style={{ fontSize: '0.875rem' }}>
                  <thead>
                    <tr>
                      <th>Asset</th>
                      <th>Amount</th>
                    </tr>
                  </thead>
                  <tbody>
                    {frozenBalancesQuery.data.map((balance) => (
                      <tr key={balance.assetId}>
                        <td>{balance.assetId}</td>
                        <td>{parseFloat(balance.amount).toFixed(8)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p>No frozen balances</p>
              )
            )}
          </div>

          <div>
            <details>
              <summary style={{ cursor: 'pointer', fontWeight: 'bold' }}>Raw JSON</summary>
              <pre style={{ background: '#000', padding: '1rem', borderRadius: '4px', overflow: 'auto', fontSize: '0.75rem' }}>
                {JSON.stringify(selectedVault, null, 2)}
              </pre>
            </details>
          </div>
        </div>
      )}
    </div>
  );
}
