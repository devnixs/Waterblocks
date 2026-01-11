import { useState } from 'react';
import { BrowserRouter, Routes, Route, Link, useNavigate, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import TransactionsPage from './pages/TransactionsPage';
import VaultsPage from './pages/VaultsPage';
import { ToastProvider } from './components/ToastProvider';
import { KeyboardShortcutsDialog } from './components/KeyboardShortcutsDialog';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
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
