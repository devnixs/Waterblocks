import { useEffect, useRef, useState } from 'react';
import logo from './assets/logo.png';
import { BrowserRouter, Routes, Route, Link, useNavigate, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import TransactionsPage from './pages/TransactionsPage';
import VaultsPage from './pages/VaultsPage';
import WorkspacesPage from './pages/WorkspacesPage';
import AssetsPage from './pages/AssetsPage';
import { ToastProvider } from './components/ToastProvider';
import { KeyboardShortcutsDialog } from './components/KeyboardShortcutsDialog';
import { LoginGate } from './components/LoginGate';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
import { useRealtimeUpdates } from './hooks/useRealtimeUpdates';
import { useCurrentUser } from './hooks/useCurrentUser';
import { useAutoTransitions, usePendingTransactionsSummary, useSetAutoTransitions, useWorkspaces } from './api/queries';
import type { PendingTransactionSummaryItem } from './types/admin';
import './App.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 3000,
      refetchOnWindowFocus: false,
    },
  },
});

const buildCommitHash = import.meta.env.VITE_APP_COMMIT_HASH?.trim();

function AppContent() {
  const navigate = useNavigate();
  const location = useLocation();
  const [showShortcuts, setShowShortcuts] = useState(false);
  const [showPendingDropdown, setShowPendingDropdown] = useState(false);
  const { email, login, logout, isLoggedIn } = useCurrentUser();
  const pendingDropdownRef = useRef<HTMLDivElement | null>(null);
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
  const pendingSummary = usePendingTransactionsSummary();
  const selectedWorkspace = workspaces.data?.find((workspace) => workspace.id === workspaceId);

  const persistWorkspaceId = (id: string) => {
    try {
      localStorage.setItem('workspaceId', id);
    } catch {
      // ignore storage errors
    }
  };

  useEffect(() => {
    if (!workspaces.data || workspaces.data.length === 0) return;
    if (!workspaceId) {
      const defaultId = workspaces.data[0].id;
      persistWorkspaceId(defaultId);
      setWorkspaceId(defaultId);
      return;
    }
    const exists = workspaces.data.some((workspace) => workspace.id === workspaceId);
    if (!exists) {
      const fallbackId = workspaces.data[0].id;
      persistWorkspaceId(fallbackId);
      setWorkspaceId(fallbackId);
    }
  }, [workspaceId, workspaces.data]);

  useEffect(() => {
    if (workspaceId) {
      persistWorkspaceId(workspaceId);
      queryClient.invalidateQueries();
    }
  }, [workspaceId]);

  useEffect(() => {
    if (!showPendingDropdown) {
      return undefined;
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (!pendingDropdownRef.current?.contains(event.target as Node)) {
        setShowPendingDropdown(false);
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
    };
  }, [showPendingDropdown]);

  useKeyboardShortcuts([
    { key: '1', handler: () => navigate('/transactions'), description: 'Navigate to Transactions' },
    { key: '2', handler: () => navigate('/vaults'), description: 'Navigate to Vaults' },
    { key: '3', handler: () => navigate('/workspaces'), description: 'Navigate to Workspaces' },
    { key: '4', handler: () => navigate('/assets'), description: 'Navigate to Assets' },
    { key: '?', handler: () => setShowShortcuts(true), description: 'Show keyboard shortcuts' },
  ]);

  const handlePendingTransactionClick = (item: PendingTransactionSummaryItem) => {
    const nextWorkspaceId = item.sourceWorkspaceId || item.destinationWorkspaceId || '';
    if (nextWorkspaceId) {
      persistWorkspaceId(nextWorkspaceId);
      setWorkspaceId(nextWorkspaceId);
    }

    setShowPendingDropdown(false);
    navigate(`/transactions/${encodeURIComponent(item.id)}`);
  };

  const formatPendingAmount = (amount: string) => {
    const numeric = Number.parseFloat(amount);
    if (Number.isNaN(numeric)) {
      return amount;
    }

    return numeric.toLocaleString(undefined, {
      minimumFractionDigits: 0,
      maximumFractionDigits: 8,
    });
  };

  if (!isLoggedIn) {
    return <LoginGate onLogin={login} />;
  }

  return (
    <div className="app">
      <header className="header">
        <h1 className="brand">
          <img src={logo} alt="Waterblocks" />
          <span>Waterblocks Admin</span>
          {buildCommitHash && (
            <span className="build-version" title="Build commit hash">
              {buildCommitHash}
            </span>
          )}
        </h1>
        <nav className="nav">
          <Link
            to="/transactions"
            className={`nav-link ${location.pathname === '/' || location.pathname.startsWith('/transactions') ? 'active' : ''}`}
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
          <Link
            to="/assets"
            className={`nav-link ${location.pathname === '/assets' ? 'active' : ''}`}
          >
            Assets
          </Link>
          <select
            value={workspaceId}
            onChange={(e) => {
              const nextId = e.target.value;
              persistWorkspaceId(nextId);
              setWorkspaceId(nextId);
            }}
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
              disabled={!workspaceId || setAutoTransitions.isPending || autoTransitions.isLoading}
              title={
                selectedWorkspace
                  ? `Auto-transition for ${selectedWorkspace.name}`
                  : 'Select a workspace to configure auto-transition'
              }
            />
            <span className="toggle-track" />
            <span className="toggle-label">Auto-transition (workspace)</span>
          </label>
          <span
            className="realtime-status"
            data-status={realtimeStatus}
            title={`Realtime: ${realtimeStatus}`}
          >
            {realtimeStatus}
          </span>
          <div className="pending-transactions" ref={pendingDropdownRef}>
            <button
              type="button"
              className="pending-transactions-trigger"
              onClick={() => setShowPendingDropdown((open) => !open)}
              aria-expanded={showPendingDropdown}
              aria-haspopup="dialog"
            >
              {pendingSummary.data?.count ?? 0} pending transactions
            </button>
            {showPendingDropdown && (
              <div className="pending-transactions-dropdown" role="dialog" aria-label="Pending transactions">
                {pendingSummary.data && pendingSummary.data.items.length > 0 ? (
                  pendingSummary.data.items.map((item) => (
                    <button
                      key={item.id}
                      type="button"
                      className="pending-transaction-row"
                      onClick={() => handlePendingTransactionClick(item)}
                    >
                      <div className="pending-transaction-row-header">
                        <span className="pending-transaction-amount">
                          {formatPendingAmount(item.amount)} {item.assetId}
                        </span>
                        <span className={`state-badge state-${item.state}`}>{item.state}</span>
                      </div>
                      <div className="pending-transaction-path">
                        <div className="pending-transaction-endpoint">
                          <span className="pending-transaction-label">Source workspace</span>
                          <span>{item.sourceWorkspaceName || 'External'}</span>
                          <span className="pending-transaction-label">Source address name</span>
                          <span>{item.sourceAddressName || 'None'}</span>
                          <span className="pending-transaction-address">{item.sourceAddress || 'External'}</span>
                        </div>
                        <div className="pending-transaction-endpoint">
                          <span className="pending-transaction-label">Destination workspace</span>
                          <span>{item.destinationWorkspaceName || 'External'}</span>
                          <span className="pending-transaction-label">Destination address name</span>
                          <span>{item.destinationAddressName || 'None'}</span>
                          <span className="pending-transaction-address">{item.destinationAddress}</span>
                        </div>
                      </div>
                    </button>
                  ))
                ) : (
                  <div className="pending-transactions-empty">No pending transactions</div>
                )}
              </div>
            )}
          </div>
          <span className="user-info">
            {email}
            <button className="btn-logout" onClick={logout} title="Logout">
              Logout
            </button>
          </span>
        </nav>
      </header>
      <main className="main">
        <Routes>
          <Route path="/" element={<TransactionsPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/transactions/:transactionId" element={<TransactionsPage />} />
          <Route path="/vaults" element={<VaultsPage />} />
          <Route path="/workspaces" element={<WorkspacesPage />} />
          <Route path="/assets" element={<AssetsPage />} />
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
