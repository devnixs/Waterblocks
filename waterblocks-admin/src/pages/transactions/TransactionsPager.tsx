import type { SetState } from './types';

type TransactionsPagerProps = {
  pageIndex: number;
  totalPages: number;
  setPageIndex: SetState<number>;
  pageSize: number;
  setPageSize: SetState<number>;
};

export function TransactionsPager({
  pageIndex,
  totalPages,
  setPageIndex,
  pageSize,
  setPageSize,
}: TransactionsPagerProps) {
  return (
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
  );
}
