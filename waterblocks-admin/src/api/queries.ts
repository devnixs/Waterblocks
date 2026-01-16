import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApi } from './adminClient';
import type {
  CreateTransactionRequest,
  CreateVaultRequest,
  CreateWalletRequest,
  AdminAutoTransitionSettings,
  CreateWorkspaceRequest,
  UpdateVaultRequest,
  CreateAdminAssetRequest,
  UpdateAdminAssetRequest,
} from '../types/admin';

function getWorkspaceId() {
  try {
    return localStorage.getItem('workspaceId') || '';
  } catch {
    return '';
  }
}

// Transactions
export function useTransactions() {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: ['transactions', workspaceId],
    queryFn: async () => {
      const response = await adminApi.getTransactions();
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
    enabled: !!workspaceId,
  });
}

export function useTransactionsPaged(params: {
  pageIndex: number;
  pageSize: number;
  assetId?: string;
  transactionId?: string;
  hash?: string;
}) {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: [
      'transactionsPaged',
      workspaceId,
      params.pageIndex,
      params.pageSize,
      params.assetId || '',
      params.transactionId || '',
      params.hash || '',
    ],
    queryFn: async () => {
      const response = await adminApi.getTransactionsPaged(params);
      if (response.error) throw new Error(response.error.message);
      return response.data;
    },
    enabled: !!workspaceId,
  });
}

export function useTransaction(id: string) {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: ['transaction', workspaceId, id],
    queryFn: async () => {
      const response = await adminApi.getTransaction(id);
      if (response.error) throw new Error(response.error.message);
      return response.data;
    },
    enabled: !!id && !!workspaceId,
  });
}

export function useCreateTransaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTransactionRequest) => adminApi.createTransaction(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['transactions'] });
      queryClient.invalidateQueries({ queryKey: ['transactionsPaged'] });
      queryClient.invalidateQueries({ queryKey: ['vaults'] });
    },
  });
}

// State transitions
export function useTransitionTransaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, action, reason }: { id: string; action: string; reason?: string }) => {
      switch (action) {
        case 'approve': return adminApi.approveTransaction(id);
        case 'sign': return adminApi.signTransaction(id);
        case 'broadcast': return adminApi.broadcastTransaction(id);
        case 'confirm': return adminApi.confirmTransaction(id);
        case 'complete': return adminApi.completeTransaction(id);
        case 'fail': return adminApi.failTransaction(id, { reason: reason || 'NETWORK_ERROR' });
        case 'reject': return adminApi.rejectTransaction(id);
        case 'cancel': return adminApi.cancelTransaction(id);
        case 'timeout': return adminApi.timeoutTransaction(id);
        default: throw new Error(`Unknown action: ${action}`);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['transactions'] });
      queryClient.invalidateQueries({ queryKey: ['transactionsPaged'] });
      queryClient.invalidateQueries({ queryKey: ['transaction'] });
    },
  });
}

// Vaults
export function useVaults() {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: ['vaults', workspaceId],
    queryFn: async () => {
      const response = await adminApi.getVaults();
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
    enabled: !!workspaceId,
  });
}

export function useVault(id: string) {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: ['vault', workspaceId, id],
    queryFn: async () => {
      const response = await adminApi.getVault(id);
      if (response.error) throw new Error(response.error.message);
      return response.data;
    },
    enabled: !!id && !!workspaceId,
  });
}

export function useCreateVault() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateVaultRequest) => adminApi.createVault(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vaults'] });
    },
  });
}

export function useUpdateVault() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateVaultRequest }) => adminApi.updateVault(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vaults'] });
    },
  });
}

export function useDeleteVault() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminApi.deleteVault(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vaults'] });
    },
  });
}

export function useCreateWallet(vaultId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateWalletRequest) => adminApi.createWallet(vaultId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vaults'] });
      queryClient.invalidateQueries({ queryKey: ['vault', vaultId] });
    },
  });
}

export function useAutoTransitions() {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: ['settings', 'autoTransitions', workspaceId],
    queryFn: async () => {
      const response = await adminApi.getAutoTransitions();
      if (response.error) throw new Error(response.error.message);
      return response.data as AdminAutoTransitionSettings;
    },
    enabled: !!workspaceId,
  });
}

export function useSetAutoTransitions() {
  const workspaceId = getWorkspaceId();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (enabled: boolean) => adminApi.setAutoTransitions(enabled),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings', 'autoTransitions', workspaceId] });
    },
  });
}

export function useFrozenBalances(vaultId: string) {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: ['vault', workspaceId, vaultId, 'frozen'],
    queryFn: async () => {
      const response = await adminApi.getFrozenBalances(vaultId);
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
    enabled: !!vaultId && !!workspaceId,
  });
}

// Assets
export function useAssets() {
  return useQuery({
    queryKey: ['assets'],
    queryFn: async () => {
      const response = await adminApi.getAssets();
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
  });
}

export function useAdminAssets() {
  const workspaceId = getWorkspaceId();
  return useQuery({
    queryKey: ['adminAssets', workspaceId],
    queryFn: async () => {
      const response = await adminApi.getAdminAssets();
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
  });
}

export function useCreateAdminAsset() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAdminAssetRequest) => adminApi.createAdminAsset(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['adminAssets'] });
      queryClient.invalidateQueries({ queryKey: ['assets'] });
    },
  });
}

export function useUpdateAdminAsset() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateAdminAssetRequest }) => (
      adminApi.updateAdminAsset(id, request)
    ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['adminAssets'] });
      queryClient.invalidateQueries({ queryKey: ['assets'] });
    },
  });
}

export function useDeleteAdminAsset() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminApi.deleteAdminAsset(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['adminAssets'] });
      queryClient.invalidateQueries({ queryKey: ['assets'] });
    },
  });
}

// Workspaces
export function useWorkspaces() {
  return useQuery({
    queryKey: ['workspaces'],
    queryFn: async () => {
      const response = await adminApi.getWorkspaces();
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
  });
}

export function useCreateWorkspace() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateWorkspaceRequest) => adminApi.createWorkspace(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workspaces'] });
    },
  });
}

export function useDeleteWorkspace() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminApi.deleteWorkspace(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['workspaces'] });
    },
  });
}
