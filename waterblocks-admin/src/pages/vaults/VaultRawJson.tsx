import type { AdminVault } from '../../types/admin';

type VaultRawJsonProps = {
  vault: AdminVault;
};

export function VaultRawJson({ vault }: VaultRawJsonProps) {
  return (
    <div className="mt-8">
      <details>
        <summary className="cursor-pointer text-xs font-bold uppercase tracking-wider text-muted mb-2">Raw JSON</summary>
        <pre className="bg-black/50 p-4 rounded-lg overflow-auto text-xs font-mono border border-tertiary">
          {JSON.stringify(vault, null, 2)}
        </pre>
      </details>
    </div>
  );
}
