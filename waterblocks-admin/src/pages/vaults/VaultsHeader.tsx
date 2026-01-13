type VaultsHeaderProps = {
  totalCount: number;
  showCreateForm: boolean;
  onToggleCreate: () => void;
};

export function VaultsHeader({ totalCount, showCreateForm, onToggleCreate }: VaultsHeaderProps) {
  return (
    <div className="flex-between mb-4">
      <h2>Vaults <span className="text-muted text-sm">({totalCount})</span></h2>
      <button
        className="btn btn-primary"
        onClick={onToggleCreate}
      >
        {showCreateForm ? 'Cancel' : '+ Create Vault'}
      </button>
    </div>
  );
}
