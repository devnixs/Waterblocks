import { useEffect, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import type { AdminTransaction, AdminVault } from '../types/admin';
import { getApiBaseUrl } from '../config/runtimeConfig';

const API_BASE_URL = getApiBaseUrl();

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

    // Each connection owns this state, so a closing connection cannot change the next one.
    let stopped = false;
    let retryTimeout: number | null = null;
    let reconnectAttempt = 0;

    const invalidateExchangeQueries = () => {
      queryClient.invalidateQueries({ queryKey: ['exchangeAccounts', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['exchangeAccount', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['exchangeTransfers', workspaceId] });
    };

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/admin?workspaceId=${encodeURIComponent(workspaceId)}`, { withCredentials: false })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('transactionUpserted', (transaction: AdminTransaction) => {
      queryClient.setQueryData<AdminTransaction[]>(['transactions', workspaceId], (prev) => upsertById(prev, transaction));
      queryClient.setQueryData<AdminTransaction>(['transaction', workspaceId, transaction.id], transaction);
      queryClient.invalidateQueries({ queryKey: ['transactionsPaged', workspaceId] });
      invalidateExchangeQueries();
    });

    connection.on('vaultUpserted', (vault: AdminVault) => {
      queryClient.setQueryData<AdminVault[]>(['vaults', workspaceId], (prev) => upsertById(prev, vault));
      queryClient.setQueryData<AdminVault>(['vault', workspaceId, vault.id], vault);
    });

    connection.on('transactionsUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['transactions', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['transaction', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['transactionsPaged', workspaceId] });
      invalidateExchangeQueries();
    });

    connection.on('vaultsUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['vaults', workspaceId] });
      queryClient.invalidateQueries({ queryKey: ['vault', workspaceId] });
      invalidateExchangeQueries();
    });

    connection.on('pendingTransactionsUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['pendingTransactionsSummary'] });
    });

    connection.on('workspacesUpdated', () => {
      queryClient.invalidateQueries({ queryKey: ['workspaces'] });
    });

    const scheduleReconnect = () => {
      if (stopped) return;
      setStatus('reconnecting');
      const delay = Math.min(30000, 1000 * Math.pow(2, reconnectAttempt));
      reconnectAttempt += 1;
      if (retryTimeout !== null) {
        window.clearTimeout(retryTimeout);
      }
      retryTimeout = window.setTimeout(() => {
        startConnection().catch(() => undefined);
      }, delay);
    };

    const startConnection = async () => {
      try {
        setStatus('connecting');
        await connection.start();
        if (!stopped) {
          reconnectAttempt = 0;
          setStatus('connected');
        }
      } catch (err) {
        if (!stopped) {
          console.warn('SignalR connection failed', err);
          scheduleReconnect();
        }
      }
    };

    connection.onreconnecting(() => setStatus('reconnecting'));
    connection.onreconnected(() => setStatus('connected'));
    connection.onclose(() => {
      if (!stopped) {
        setStatus('disconnected');
        scheduleReconnect();
      }
    });

    // A short delay lets a mount that React discards at once end before negotiation starts.
    retryTimeout = window.setTimeout(() => {
      startConnection().catch(() => undefined);
    }, 0);

    return () => {
      stopped = true;
      if (retryTimeout !== null) {
        window.clearTimeout(retryTimeout);
      }
      connection.stop().catch(() => undefined);
    };
  }, [queryClient, workspaceId]);

  return status;
}
