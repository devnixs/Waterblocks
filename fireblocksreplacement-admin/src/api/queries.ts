import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApi } from './adminClient';
import type { CreateTransactionRequest, CreateVaultRequest, CreateWalletRequest, AdminAutoTransitionSettings } from '../types/admin';

// Transactions
export function useTransactions() {
  return useQuery({
    queryKey: ['transactions'],
    queryFn: async () => {
      const response = await adminApi.getTransactions();
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
  });
}

export function useTransaction(id: string) {
  return useQuery({
    queryKey: ['transaction', id],
    queryFn: async () => {
      const response = await adminApi.getTransaction(id);
      if (response.error) throw new Error(response.error.message);
      return response.data;
    },
    enabled: !!id,
  });
}

export function useCreateTransaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTransactionRequest) => adminApi.createTransaction(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['transactions'] });
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
      queryClient.invalidateQueries({ queryKey: ['transaction'] });
    },
  });
}

// Vaults
export function useVaults() {
  return useQuery({
    queryKey: ['vaults'],
    queryFn: async () => {
      const response = await adminApi.getVaults();
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
  });
}

export function useVault(id: string) {
  return useQuery({
    queryKey: ['vault', id],
    queryFn: async () => {
      const response = await adminApi.getVault(id);
      if (response.error) throw new Error(response.error.message);
      return response.data;
    },
    enabled: !!id,
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
  return useQuery({
    queryKey: ['settings', 'autoTransitions'],
    queryFn: async () => {
      const response = await adminApi.getAutoTransitions();
      if (response.error) throw new Error(response.error.message);
      return response.data as AdminAutoTransitionSettings;
    },
  });
}

export function useSetAutoTransitions() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (enabled: boolean) => adminApi.setAutoTransitions(enabled),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings', 'autoTransitions'] });
    },
  });
}

export function useFrozenBalances(vaultId: string) {
  return useQuery({
    queryKey: ['vault', vaultId, 'frozen'],
    queryFn: async () => {
      const response = await adminApi.getFrozenBalances(vaultId);
      if (response.error) throw new Error(response.error.message);
      return response.data || [];
    },
    enabled: !!vaultId,
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
