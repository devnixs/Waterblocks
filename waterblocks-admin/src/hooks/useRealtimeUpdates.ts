import { useEffect, useRef, useState } from 'react';
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
  const retryTimeoutRef = useRef<number | null>(null);
  const reconnectAttemptRef = useRef(0);
  const stoppedRef = useRef(false);

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

    const scheduleReconnect = () => {
      if (stoppedRef.current) return;
      setStatus('reconnecting');
      const attempt = reconnectAttemptRef.current;
      const delay = Math.min(30000, 1000 * Math.pow(2, attempt));
      reconnectAttemptRef.current += 1;
      if (retryTimeoutRef.current) {
        window.clearTimeout(retryTimeoutRef.current);
      }
      retryTimeoutRef.current = window.setTimeout(() => {
        startConnection().catch(() => undefined);
      }, delay);
    };

    const startConnection = async () => {
      try {
        setStatus('connecting');
        await connection.start();
        if (!stoppedRef.current) {
          reconnectAttemptRef.current = 0;
          setStatus('connected');
        }
      } catch (err) {
        if (!stoppedRef.current) {
          console.warn('SignalR connection failed', err);
          scheduleReconnect();
        }
      }
    };

    connection.onreconnecting(() => setStatus('reconnecting'));
    connection.onreconnected(() => setStatus('connected'));
    connection.onclose(() => {
      if (!stoppedRef.current) {
        setStatus('disconnected');
        scheduleReconnect();
      }
    });

    stoppedRef.current = false;
    startConnection().catch(() => undefined);

    return () => {
      stoppedRef.current = true;
      if (retryTimeoutRef.current) {
        window.clearTimeout(retryTimeoutRef.current);
        retryTimeoutRef.current = null;
      }
      connection.stop().catch(() => undefined);
    };
  }, [queryClient, workspaceId]);

  return status;
}
