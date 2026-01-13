type BulkConfirmDialogProps = {
  action: string;
  count: number;
  onCancel: () => void;
  onConfirm: () => void;
};

export function BulkConfirmDialog({ action, count, onCancel, onConfirm }: BulkConfirmDialogProps) {
  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 backdrop-blur-sm">
      <div className="bg-secondary border border-tertiary rounded-lg p-6 max-w-md w-full shadow-2xl animate-in fade-in zoom-in duration-200">
        <h3 className="text-lg font-bold mb-2">Confirm Bulk Action</h3>
        <p className="text-muted mb-6">
          Are you sure you want to <strong>{action}</strong> {count} transaction(s)?
        </p>
        <div className="flex justify-end gap-3">
          <button className="btn btn-secondary" onClick={onCancel}>
            Cancel
          </button>
          <button className="btn btn-danger" onClick={onConfirm}>
            Confirm {action}
          </button>
        </div>
      </div>
    </div>
  );
}
