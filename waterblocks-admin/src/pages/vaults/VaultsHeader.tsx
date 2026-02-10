type VaultsHeaderProps = {
  totalCount: number;
  showCreateForm: boolean;
  onToggleCreate: () => void;
  showArchived: boolean;
  onToggleShowArchived: () => void;
};

export function VaultsHeader({
  totalCount,
  showCreateForm,
  onToggleCreate,
  showArchived,
  onToggleShowArchived,
}: VaultsHeaderProps) {
  return (
    <div className="flex-between mb-4">
      <h2>Vaults <span className="text-muted text-sm">({totalCount})</span></h2>
      <div className="flex items-center gap-4">
        <label className="flex items-center gap-2 text-sm text-muted cursor-pointer select-none">
          <input
            type="checkbox"
            checked={showArchived}
            onChange={onToggleShowArchived}
          />
          Show archived only
        </label>
        <button
          className="btn btn-primary"
          onClick={onToggleCreate}
        >
          {showCreateForm ? 'Cancel' : '+ Create Vault'}
        </button>
      </div>
    </div>
  );
}
