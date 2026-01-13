type HeaderProps = {
  totalCount: number;
  showCreateForm: boolean;
  onToggleCreate: () => void;
  selectedCount: number;
  onBulkAction: (action: string) => void;
  onClearSelection: () => void;
};

export function TransactionsHeader({
  totalCount,
  showCreateForm,
  onToggleCreate,
  selectedCount,
  onBulkAction,
  onClearSelection,
}: HeaderProps) {
  return (
    <div className="flex-between mb-4">
      <h2>
        Transactions <span className="text-muted text-sm">({totalCount})</span>
      </h2>
      <div className="flex-gap-4">
        <button
          className="btn btn-primary"
          onClick={onToggleCreate}
        >
          {showCreateForm ? 'Close' : '+ New Transaction'}
        </button>

        {selectedCount > 0 && (
          <div className="flex-gap-2 items-center bg-secondary p-2 rounded-md">
            <span className="text-muted text-sm px-2">
              {selectedCount} selected
            </span>
            <button className="btn btn-primary" onClick={() => onBulkAction('approve')}>
              Approve
            </button>
            <button className="btn btn-primary" onClick={() => onBulkAction('sign')}>
              Sign
            </button>
            <button className="btn btn-primary" onClick={() => onBulkAction('complete')}>
              Complete
            </button>
            <button className="btn btn-danger" onClick={onClearSelection}>
              Clear
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
