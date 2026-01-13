type CreateVaultFormProps = {
  vaultName: string;
  setVaultName: (value: string) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
};

export function CreateVaultForm({
  vaultName,
  setVaultName,
  onSubmit,
  isSubmitting,
}: CreateVaultFormProps) {
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit();
      }}
      className="card"
    >
      <h3 className="mb-4 text-lg font-semibold">Create New Vault</h3>
      <div className="flex gap-2">
        <input
          type="text"
          placeholder="Vault name"
          value={vaultName}
          onChange={(e) => setVaultName(e.target.value)}
          className="flex-1"
        />
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isSubmitting}
        >
          Create
        </button>
      </div>
    </form>
  );
}
