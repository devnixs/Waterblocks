import * as Toast from '@radix-ui/react-toast';
import { createContext, useContext, useState, ReactNode } from 'react';

interface ToastMessage {
  id: string;
  title: string;
  description?: string;
  duration?: number;
  type?: 'success' | 'error' | 'info';
}

interface ToastContextType {
  showToast: (message: Omit<ToastMessage, 'id'>) => void;
}

const ToastContext = createContext<ToastContextType | undefined>(undefined);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const showToast = (message: Omit<ToastMessage, 'id'>) => {
    const id = Math.random().toString(36).substring(7);
    setToasts((prev) => [...prev, { ...message, id }]);
  };

  const removeToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  return (
    <ToastContext.Provider value={{ showToast }}>
      <Toast.Provider swipeDirection="right">
        {children}
        {toasts.map((toast) => (
          <Toast.Root
            key={toast.id}
            duration={toast.duration || 3000}
            onOpenChange={(open) => {
              if (!open) removeToast(toast.id);
            }}
            className={`toast toast-${toast.type || 'info'}`}
            style={{
              position: 'fixed',
              bottom: '20px',
              right: '20px',
              background: toast.type === 'error' ? '#ef4444' : toast.type === 'success' ? '#10b981' : '#3b82f6',
              color: 'white',
              padding: '1rem 1.5rem',
              borderRadius: '8px',
              boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
              maxWidth: '400px',
              zIndex: 9999,
              animation: 'slideIn 0.2s ease-out',
            }}
          >
            <Toast.Title style={{ fontWeight: 600, marginBottom: toast.description ? '0.5rem' : 0 }}>
              {toast.title}
            </Toast.Title>
            {toast.description && (
              <Toast.Description style={{ fontSize: '0.875rem', opacity: 0.9 }}>
                {toast.description}
              </Toast.Description>
            )}
            <Toast.Close
              style={{
                position: 'absolute',
                top: '0.5rem',
                right: '0.5rem',
                background: 'transparent',
                border: 'none',
                color: 'white',
                cursor: 'pointer',
                fontSize: '1.25rem',
                lineHeight: 1,
                opacity: 0.7,
              }}
              aria-label="Close"
            >
              ×
            </Toast.Close>
          </Toast.Root>
        ))}
        <Toast.Viewport />
      </Toast.Provider>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within ToastProvider');
  }
  return context;
}
