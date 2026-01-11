import { useState } from 'react';
import { useWorkspaces, useCreateWorkspace, useDeleteWorkspace } from '../api/queries';
import { useToast } from '../components/ToastProvider';

export default function WorkspacesPage() {
  const { data: workspaces, isLoading, error } = useWorkspaces();
  const createWorkspace = useCreateWorkspace();
  const deleteWorkspace = useDeleteWorkspace();
  const { showToast } = useToast();
  const [name, setName] = useState('');

  const handleCreate = async () => {
    if (!name.trim()) {
      showToast({ title: 'Workspace name is required', type: 'error' });
      return;
    }

    const result = await createWorkspace.mutateAsync({ name: name.trim() });
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    setName('');
    showToast({ title: 'Workspace created', type: 'success', duration: 2500 });
  };

  const handleDelete = async (id: string) => {
    const confirmed = confirm('Delete this workspace? This removes its vaults and transactions.');
    if (!confirmed) return;

    const result = await deleteWorkspace.mutateAsync(id);
    if (result.error) {
      showToast({ title: `Error: ${result.error.message}`, type: 'error', duration: 5000 });
      return;
    }

    showToast({ title: 'Workspace deleted', type: 'success', duration: 2500 });
  };

  if (isLoading) return <div className="p-8 text-center text-muted">Loading workspaces...</div>;
  if (error) return <div className="p-8 text-center text-red-500">Error: {error.message}</div>;

  return (
    <div>
      <div className="flex-between mb-4">
        <h2>Workspaces <span className="text-muted text-sm">({workspaces?.length || 0})</span></h2>
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          handleCreate();
        }}
        className="card mb-6"
      >
        <h3 className="mb-4 text-lg font-semibold">Create Workspace</h3>
        <div className="flex gap-2">
          <input
            type="text"
            placeholder="Workspace name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="flex-1"
          />
          <button
            type="submit"
            className="btn btn-primary"
            disabled={createWorkspace.isPending}
          >
            Create
          </button>
        </div>
      </form>

      <div className="grid gap-4">
        {(workspaces || []).map((workspace) => (
          <div key={workspace.id} className="card">
            <div className="flex-between mb-2">
              <div>
                <div className="text-lg font-semibold">{workspace.name}</div>
                <div className="text-muted text-sm text-mono">{workspace.id}</div>
              </div>
              <button
                className="btn btn-danger"
                onClick={() => handleDelete(workspace.id)}
                disabled={deleteWorkspace.isPending}
              >
                Delete
              </button>
            </div>
            <div className="text-sm text-muted mb-3">
              Created {new Date(workspace.createdAt).toLocaleString()}
            </div>
            <div>
              <div className="text-xs uppercase tracking-wider text-muted font-bold mb-2">API Keys</div>
              {workspace.apiKeys.length > 0 ? (
                <div className="grid gap-2">
                  {workspace.apiKeys.map((key) => (
                    <div key={key.id} className="p-3 border border-tertiary rounded-lg">
                      <div className="flex-between">
                        <div className="font-medium">{key.name}</div>
                        <div className="text-muted text-xs">{new Date(key.createdAt).toLocaleString()}</div>
                      </div>
                      <div className="text-mono text-sm mt-1">{key.key}</div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-sm text-muted">No API keys</div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
