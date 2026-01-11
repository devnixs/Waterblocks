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

export function useRealtimeUpdates() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<'connecting' | 'connected' | 'reconnecting' | 'disconnected'>('connecting');

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/admin`, { withCredentials: false })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('transactionUpserted', (transaction: AdminTransaction) => {
      queryClient.setQueryData<AdminTransaction[]>(['transactions'], (prev) => upsertById(prev, transaction));
      queryClient.setQueryData<AdminTransaction>(['transaction', transaction.id], transaction);
    });

    connection.on('vaultUpserted', (vault: AdminVault) => {
      queryClient.setQueryData<AdminVault[]>(['vaults'], (prev) => upsertById(prev, vault));
      queryClient.setQueryData<AdminVault>(['vault', vault.id], vault);
    });

    connection.on('transactionsUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['transactions'] });
      queryClient.invalidateQueries({ queryKey: ['transaction'] });
    });

    connection.on('vaultsUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['vaults'] });
      queryClient.invalidateQueries({ queryKey: ['vault'] });
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
  }, [queryClient]);

  return status;
}
