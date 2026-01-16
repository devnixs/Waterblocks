import type {
  AdminResponse,
  AdminTransaction,
  AdminTransactionsPage,
  AdminVault,
  CreateTransactionRequest,
  FailTransactionRequest,
  CreateVaultRequest,
  UpdateVaultRequest,
  FrozenBalance,
  CreateWalletRequest,
  AdminWallet,
  AdminAutoTransitionSettings,
  Asset,
  AdminAsset,
  CreateAdminAssetRequest,
  UpdateAdminAssetRequest,
  AdminWorkspace,
  CreateWorkspaceRequest,
  AdminGeneratedAddress,
} from '../types/admin';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5671';

function getWorkspaceId() {
  try {
    return localStorage.getItem('workspaceId') || '';
  } catch {
    return '';
  }
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<AdminResponse<T>> {
  const workspaceId = endpoint.startsWith('/admin') ? getWorkspaceId() : '';
  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(workspaceId ? { 'X-Workspace-Id': workspaceId } : {}),
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

  async getTransactionsPaged(params: {
    pageIndex: number;
    pageSize: number;
    assetId?: string;
    transactionId?: string;
    hash?: string;
  }): Promise<AdminResponse<AdminTransactionsPage>> {
    const search = new URLSearchParams({
      pageIndex: String(params.pageIndex),
      pageSize: String(params.pageSize),
    });
    if (params.assetId) search.set('assetId', params.assetId);
    if (params.transactionId) search.set('transactionId', params.transactionId);
    if (params.hash) search.set('hash', params.hash);

    return fetchApi(`/admin/transactions/paged?${search.toString()}`);
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

  async updateVault(id: string, request: UpdateVaultRequest): Promise<AdminResponse<AdminVault>> {
    return fetchApi(`/admin/vaults/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(request),
    });
  },

  async deleteVault(id: string): Promise<AdminResponse<boolean>> {
    return fetchApi(`/admin/vaults/${id}`, { method: 'DELETE' });
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

  // Assets
  async getAssets(): Promise<AdminResponse<Asset[]>> {
    const response = await fetch(`${API_BASE_URL}/supported_assets`, {
      headers: { 'Content-Type': 'application/json' },
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({
        data: null,
        error: { message: `HTTP ${response.status}`, code: 'HTTP_ERROR' },
      }));
      return error;
    }

    const data = await response.json();
    return { data, error: null };
  },

  async getAdminAssets(): Promise<AdminResponse<AdminAsset[]>> {
    return fetchApi('/admin/assets');
  },

  async createAdminAsset(request: CreateAdminAssetRequest): Promise<AdminResponse<AdminAsset>> {
    return fetchApi('/admin/assets', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async updateAdminAsset(id: string, request: UpdateAdminAssetRequest): Promise<AdminResponse<AdminAsset>> {
    return fetchApi(`/admin/assets/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(request),
    });
  },

  async deleteAdminAsset(id: string): Promise<AdminResponse<boolean>> {
    return fetchApi(`/admin/assets/${id}`, { method: 'DELETE' });
  },

  async generateAddress(assetId: string): Promise<AdminResponse<AdminGeneratedAddress>> {
    const search = new URLSearchParams({ assetId });
    return fetchApi(`/admin/addresses/generate?${search.toString()}`);
  },

  // Workspaces
  async getWorkspaces(): Promise<AdminResponse<AdminWorkspace[]>> {
    return fetchApi('/admin/workspaces');
  },

  async createWorkspace(request: CreateWorkspaceRequest): Promise<AdminResponse<AdminWorkspace>> {
    return fetchApi('/admin/workspaces', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async deleteWorkspace(id: string): Promise<AdminResponse<boolean>> {
    return fetchApi(`/admin/workspaces/${id}`, { method: 'DELETE' });
  },
};
