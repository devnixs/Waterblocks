import { useState } from 'react';
import { BrowserRouter, Routes, Route, Link, useNavigate, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import TransactionsPage from './pages/TransactionsPage';
import VaultsPage from './pages/VaultsPage';
import { ToastProvider } from './components/ToastProvider';
import { KeyboardShortcutsDialog } from './components/KeyboardShortcutsDialog';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
import { useRealtimeUpdates } from './hooks/useRealtimeUpdates';
import { useAutoTransitions, useSetAutoTransitions } from './api/queries';
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

  const realtimeStatus = useRealtimeUpdates();
  const autoTransitions = useAutoTransitions();
  const setAutoTransitions = useSetAutoTransitions();

  useKeyboardShortcuts([
    { key: '1', handler: () => navigate('/transactions'), description: 'Navigate to Transactions' },
    { key: '2', handler: () => navigate('/vaults'), description: 'Navigate to Vaults' },
    { key: '?', handler: () => setShowShortcuts(true), description: 'Show keyboard shortcuts' },
  ]);

  return (
    <div className="app">
      <header className="header">
        <h1>FireblocksReplacement Admin</h1>
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
          <button
            onClick={() => setShowShortcuts(true)}
            className="nav-link"
            style={{ background: 'none', border: 'none', cursor: 'pointer' }}
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
