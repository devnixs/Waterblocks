import { useEffect, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import type { AdminTransaction, AdminVault } from '../types/admin';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5671';

function upsertById<T extends { id: string }>(items: T[] | undefined, next: T): T[] {
  if (!items) return [next];
  const index = items.findIndex((item) => item.id === next.id);
  if (index === -1) return [next, ...items];
  const clone = items.slice();
  clone[index] = next;
  return clone;
}

export function useRealtimeUpdates(workspaceId?: string) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<'connecting' | 'connected' | 'reconnecting' | 'disconnected'>('connecting');

  useEffect(() => {
    if (!workspaceId) {
      setStatus('disconnected');
      return undefined;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/admin?workspaceId=${encodeURIComponent(workspaceId)}`, { withCredentials: false })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('transactionUpserted', (transaction: AdminTransaction) => {
      queryClient.setQueryData<AdminTransaction[]>(['transactions', workspaceId], (prev) => upsertById(prev, transaction));
      queryClient.setQueryData<AdminTransaction>(['transaction', workspaceId, transaction.id], transaction);
      queryClient.invalidateQueries({ queryKey: ['transactionsPaged', workspaceId] });
    });

    connection.on('vaultUpserted', (vault: AdminVault) => {
      queryClient.setQueryData<AdminVault[]>(['vaults', workspaceId], (prev) => upsertById(prev, vault));
      queryClient.setQueryData<AdminVault>(['vault', workspaceId, vault.id], vault);
    });

    connection.on('transactionsUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['transactions', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['transaction', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['transactionsPaged', workspaceId] });
    });

    connection.on('vaultsUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['vaults', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['vault', workspaceId] });
    });

    connection.onreconnecting(() => setStatus('reconnecting'));
    connection.onreconnected(() => setStatus('connected'));
    connection.onclose(() => setStatus('disconnected'));

    let isMounted = true;
    connection.start().then(() => {
      if (isMounted) setStatus('connected');
    }).catch((err) => {
      if (isMounted) {
        setStatus('disconnected');
        console.warn('SignalR connection failed', err);
      }
    });

    return () => {
      isMounted = false;
      connection.stop().catch(() => undefined);
    };
  }, [queryClient, workspaceId]);

  return status;
}
