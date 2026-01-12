import { useEffect, useState } from 'react';
import { BrowserRouter, Routes, Route, Link, useNavigate, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import TransactionsPage from './pages/TransactionsPage';
import VaultsPage from './pages/VaultsPage';
import WorkspacesPage from './pages/WorkspacesPage';
import { ToastProvider } from './components/ToastProvider';
import { KeyboardShortcutsDialog } from './components/KeyboardShortcutsDialog';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
import { useRealtimeUpdates } from './hooks/useRealtimeUpdates';
import { useAutoTransitions, useSetAutoTransitions, useWorkspaces } from './api/queries';
import './App.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 3000,
      refetchOnWindowFocus: false,
    },
  },
});

function AppContent() {
  const navigate = useNavigate();
  const location = useLocation();
  const [showShortcuts, setShowShortcuts] = useState(false);
  const [workspaceId, setWorkspaceId] = useState(() => {
    try {
      return localStorage.getItem('workspaceId') || '';
    } catch {
      return '';
    }
  });

  const workspaces = useWorkspaces();
  const realtimeStatus = useRealtimeUpdates(workspaceId);
  const autoTransitions = useAutoTransitions();
  const setAutoTransitions = useSetAutoTransitions();

  useEffect(() => {
    if (!workspaces.data || workspaces.data.length === 0) return;
    if (!workspaceId) {
      setWorkspaceId(workspaces.data[0].id);
      return;
    }
    const exists = workspaces.data.some((workspace) => workspace.id === workspaceId);
    if (!exists) {
      setWorkspaceId(workspaces.data[0].id);
    }
  }, [workspaceId, workspaces.data]);

  useEffect(() => {
    if (workspaceId) {
      try {
        localStorage.setItem('workspaceId', workspaceId);
      } catch {
        // ignore storage errors
      }
      queryClient.invalidateQueries();
    }
  }, [workspaceId]);

  useKeyboardShortcuts([
    { key: '1', handler: () => navigate('/transactions'), description: 'Navigate to Transactions' },
    { key: '2', handler: () => navigate('/vaults'), description: 'Navigate to Vaults' },
    { key: '3', handler: () => navigate('/workspaces'), description: 'Navigate to Workspaces' },
    { key: '?', handler: () => setShowShortcuts(true), description: 'Show keyboard shortcuts' },
  ]);

  return (
    <div className="app">
      <header className="header">
        <h1>Waterblocks Admin</h1>
        <nav className="nav">
          <Link
            to="/transactions"
            className={`nav-link ${location.pathname === '/transactions' || location.pathname === '/' ? 'active' : ''}`}
          >
            Transactions
          </Link>
          <Link
            to="/vaults"
            className={`nav-link ${location.pathname === '/vaults' ? 'active' : ''}`}
          >
            Vaults
          </Link>
          <Link
            to="/workspaces"
            className={`nav-link ${location.pathname === '/workspaces' ? 'active' : ''}`}
          >
            Workspaces
          </Link>
          <select
            value={workspaceId}
            onChange={(e) => setWorkspaceId(e.target.value)}
            className="workspace-select"
            title="Active workspace"
          >
            {(workspaces.data || []).map((workspace) => (
              <option key={workspace.id} value={workspace.id}>
                {workspace.name}
              </option>
            ))}
          </select>
          <button
            onClick={() => setShowShortcuts(true)}
            className="btn-icon"
            title="Keyboard shortcuts (?)"
          >
            ?
          </button>
          <label className="toggle">
            <input
              type="checkbox"
              checked={autoTransitions.data?.enabled ?? false}
              onChange={(e) => setAutoTransitions.mutate(e.target.checked)}
              disabled={setAutoTransitions.isPending || autoTransitions.isLoading}
            />
            <span className="toggle-track" />
            <span className="toggle-label">Auto-transition</span>
          </label>
          <span
            className="realtime-status"
            data-status={realtimeStatus}
            title={`Realtime: ${realtimeStatus}`}
          >
            {realtimeStatus}
          </span>
        </nav>
      </header>
      <main className="main">
        <Routes>
          <Route path="/" element={<TransactionsPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/vaults" element={<VaultsPage />} />
          <Route path="/workspaces" element={<WorkspacesPage />} />
        </Routes>
      </main>
      <KeyboardShortcutsDialog open={showShortcuts} onOpenChange={setShowShortcuts} />
    </div>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <BrowserRouter>
          <AppContent />
        </BrowserRouter>
        <ReactQueryDevtools initialIsOpen={false} />
      </ToastProvider>
    </QueryClientProvider>
  );
}

export default App;
