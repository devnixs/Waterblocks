import type { AdminVault } from '../../types/admin';

type VaultsTableProps = {
  vaults: AdminVault[];
  onSelect: (vault: AdminVault) => void;
};

export function VaultsTable({ vaults, onSelect }: VaultsTableProps) {
  return (
    <div className="overflow-x-auto rounded-lg border border-tertiary">
      <table className="w-full">
        <thead>
          <tr>
            <th>ID</th>
            <th>Name</th>
            <th>Assets</th>
            <th>Hidden</th>
            <th>Created</th>
            <th className="text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          {vaults.map((vault) => (
            <tr
              key={vault.id}
              onClick={() => onSelect(vault)}
              className="cursor-pointer hover:bg-white/5 transition-colors"
            >
              <td className="text-mono text-sm text-muted">
                {vault.id.substring(0, 8)}...
              </td>
              <td className="font-medium">{vault.name}</td>
              <td className="text-mono">{vault.wallets.length}</td>
              <td>{vault.hiddenOnUI ? 'Yes' : 'No'}</td>
              <td className="text-sm text-muted">{new Date(vault.createdAt).toLocaleString()}</td>
              <td className="text-right">
                <button
                  className="btn btn-ghost text-sm py-1 px-3"
                  onClick={(e) => {
                    e.stopPropagation();
                    onSelect(vault);
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
  );
}
