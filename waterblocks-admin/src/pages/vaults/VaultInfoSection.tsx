import type { AdminVault } from '../../types/admin';

type VaultInfoSectionProps = {
  vault: AdminVault;
};

export function VaultInfoSection({ vault }: VaultInfoSectionProps) {
  return (
    <div className="mb-8">
      <h3 className="text-sm uppercase tracking-wider text-muted font-bold mb-4">Information</h3>
      <div className="grid gap-4 p-4 bg-tertiary/20 rounded-lg border border-tertiary">
        <div className="flex justify-between">
          <span className="text-muted">ID</span>
          <span className="text-mono select-all">{vault.id}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted">Name</span>
          <span className="font-medium">{vault.name}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted">Hidden</span>
          <span>{vault.hiddenOnUI ? 'Yes' : 'No'}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted">Auto Fuel</span>
          <span>{vault.autoFuel ? 'Yes' : 'No'}</span>
        </div>
        {vault.customerRefId && (
          <div className="flex justify-between">
            <span className="text-muted">Customer Ref</span>
            <span className="text-mono">{vault.customerRefId}</span>
          </div>
        )}
        <div className="flex justify-between">
          <span className="text-muted">Created</span>
          <span className="text-sm">{new Date(vault.createdAt).toLocaleString()}</span>
        </div>
      </div>
    </div>
  );
}
