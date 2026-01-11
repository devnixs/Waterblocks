import * as Toast from '@radix-ui/react-toast';
import { createContext, useContext, useState } from 'react';
import type { ReactNode } from 'react';

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
            className={`toast-root toast-${toast.type || 'info'}`}
          >
            <Toast.Title className="toast-title">
              {toast.title}
            </Toast.Title>
            {toast.description && (
              <Toast.Description className="toast-description">
                {toast.description}
              </Toast.Description>
            )}
            <Toast.Close
              className="toast-close"
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
