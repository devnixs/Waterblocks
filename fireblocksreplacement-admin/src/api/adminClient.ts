import type {
  AdminResponse,
  AdminTransaction,
  AdminVault,
  CreateTransactionRequest,
  FailTransactionRequest,
  CreateVaultRequest,
  FrozenBalance,
  CreateWalletRequest,
  AdminWallet,
  AdminAutoTransitionSettings,
} from '../types/admin';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<AdminResponse<T>> {
  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({
      data: null,
      error: { message: `HTTP ${response.status}`, code: 'HTTP_ERROR' },
    }));
    return error;
  }

  return response.json();
}

export const adminApi = {
  // Transactions
  async getTransactions(): Promise<AdminResponse<AdminTransaction[]>> {
    return fetchApi('/admin/transactions');
  },

  async getTransaction(id: string): Promise<AdminResponse<AdminTransaction>> {
    return fetchApi(`/admin/transactions/${id}`);
  },

  async createTransaction(request: CreateTransactionRequest): Promise<AdminResponse<AdminTransaction>> {
    return fetchApi('/admin/transactions', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  // State transitions
  async approveTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/approve`, { method: 'POST' });
  },

  async signTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/sign`, { method: 'POST' });
  },

  async broadcastTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/broadcast`, { method: 'POST' });
  },

  async confirmTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/confirm`, { method: 'POST' });
  },

  async completeTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/complete`, { method: 'POST' });
  },

  async failTransaction(id: string, request: FailTransactionRequest): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/fail`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async rejectTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/reject`, { method: 'POST' });
  },

  async cancelTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/cancel`, { method: 'POST' });
  },

  async timeoutTransaction(id: string): Promise<AdminResponse<{ id: string; state: string }>> {
    return fetchApi(`/admin/transactions/${id}/timeout`, { method: 'POST' });
  },

  // Vaults
  async getVaults(): Promise<AdminResponse<AdminVault[]>> {
    return fetchApi('/admin/vaults');
  },

  async getVault(id: string): Promise<AdminResponse<AdminVault>> {
    return fetchApi(`/admin/vaults/${id}`);
  },

  async createVault(request: CreateVaultRequest): Promise<AdminResponse<AdminVault>> {
    return fetchApi('/admin/vaults', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async getFrozenBalances(id: string): Promise<AdminResponse<FrozenBalance[]>> {
    return fetchApi(`/admin/vaults/${id}/frozen`);
  },

  async createWallet(vaultId: string, request: CreateWalletRequest): Promise<AdminResponse<AdminWallet>> {
    return fetchApi(`/admin/vaults/${vaultId}/wallets`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async getAutoTransitions(): Promise<AdminResponse<AdminAutoTransitionSettings>> {
    return fetchApi('/admin/settings/auto-transitions');
  },

  async setAutoTransitions(enabled: boolean): Promise<AdminResponse<AdminAutoTransitionSettings>> {
    return fetchApi('/admin/settings/auto-transitions', {
      method: 'POST',
      body: JSON.stringify({ enabled }),
    });
  },
};
