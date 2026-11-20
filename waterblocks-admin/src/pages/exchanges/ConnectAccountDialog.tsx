import { useState } from 'react';
import type { FormEvent } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { adminApi } from '../../api/adminClient';
import { useExchanges } from '../../api/queries';
import type { AdminExchange, AdminExchangeAccount } from '../../types/admin';
import { encryptCredentials } from './credentials';
import { findExchange, findFullExchanges } from './exchangeFormat';

const EXCHANGE_LABEL = 'Off Exchange';

type ConnectAccountDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConnected: (account: AdminExchangeAccount) => void;
  account?: AdminExchangeAccount;
  accounts?: AdminExchangeAccount[];
};

type ConnectAccountFormProps = Omit<ConnectAccountDialogProps, 'open'>;

type ExchangeStepProps = {
  exchanges: AdminExchange[];
  fullExchanges: AdminExchange[];
  onSelect: (exchange: AdminExchange) => void;
  onCancel: () => void;
};

function ExchangeStep({ exchanges, fullExchanges, onSelect, onCancel }: ExchangeStepProps) {
  return (
    <div className="grid gap-4">
      <div className="grid gap-2">
        {exchanges.length === 0 && (
          <div className="text-sm text-muted">
            No exchange has a simulator URL. Set one on the{' '}
            <Link to="/exchanges/supported" onClick={onCancel}>Supported exchanges</Link> page.
          </div>
        )}
        {exchanges.map((exchange) => {
          const isFull = fullExchanges.includes(exchange);
          return (
            <button
              key={exchange.exchangeType}
              type="button"
              className="exchange-provider"
              onClick={() => onSelect(exchange)}
              disabled={isFull}
            >
              <span className="font-medium">{exchange.name}</span>
              <span className="text-sm text-muted">{isFull ? 'This workspace already has an account' : EXCHANGE_LABEL}</span>
            </button>
          );
        })}
      </div>
      <div className="flex justify-end">
        <button type="button" className="btn btn-secondary" onClick={onCancel}>Cancel</button>
      </div>
    </div>
  );
}

async function encryptForWorkspace(apiKey: string, apiSecret: string): Promise<string> {
  const response = await adminApi.getCredentialsPublicKey();
  if (response.error || !response.data) {
    throw new Error(response.error?.message ?? 'The credentials public key cannot be read');
  }

  return encryptCredentials(response.data.publicKey, apiKey, apiSecret, response.data.tenantId);
}

function ConnectAccountForm({ onOpenChange, onConnected, account, accounts = [] }: ConnectAccountFormProps) {
  const queryClient = useQueryClient();
  const exchangesQuery = useExchanges();
  const [selectedExchange, setSelectedExchange] = useState<AdminExchange | null>(null);
  const [displayName, setDisplayName] = useState(account?.name ?? '');
  const [apiKey, setApiKey] = useState('');
  const [apiSecret, setApiSecret] = useState('');
  const [showSecret, setShowSecret] = useState(false);
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (exchangesQuery.isLoading) {
    return <div className="text-sm text-muted">Loading exchanges...</div>;
  }

  if (exchangesQuery.error) {
    return <div className="form-error" role="alert">Error: {exchangesQuery.error.message}</div>;
  }

  const connectable = (exchangesQuery.data ?? []).filter((exchange) => exchange.isConnectable);
  const exchange = account ? findExchange(connectable, account.exchangeType) : selectedExchange;
  if (account && !exchange) {
    return <div className="form-error" role="alert">The exchange of this account has no simulator URL.</div>;
  }

  if (!exchange) {
    return (
      <ExchangeStep
        exchanges={connectable}
        fullExchanges={findFullExchanges(connectable, accounts)}
        onSelect={setSelectedExchange}
        onCancel={() => onOpenChange(false)}
      />
    );
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setIsSubmitting(true);
    try {
      const key = apiKey.trim();
      const creds = await encryptForWorkspace(key, apiSecret.trim());
      const credentials = { apiKey: key, creds };
      const response = account
        ? await adminApi.connectExchangeAccount(account.id, credentials)
        : await adminApi.createExchangeAccount({
          ...credentials,
          exchangeType: exchange.exchangeType,
          name: displayName,
        });

      if (response.error || !response.data) {
        setError(response.error?.message ?? 'The account could not be connected');
        return;
      }

      queryClient.invalidateQueries({ queryKey: ['exchangeAccounts'] });
      queryClient.invalidateQueries({ queryKey: ['exchangeAccount'] });
      onOpenChange(false);
      onConnected(response.data);
    } catch (failure) {
      setError(failure instanceof Error && failure.message ? failure.message : 'The Waterblocks API cannot be reached');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="grid gap-4" autoComplete="off">
      <div className="exchange-detail-row">
        <span className="font-medium">{exchange.name}</span>
        <span className="text-muted">Main account</span>
      </div>
      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="exchange-display-name">Display name</label>
        <input
          id="exchange-display-name"
          type="text"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
          readOnly={!!account}
          required
        />
      </div>
      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="exchange-api-key">API key</label>
        <input
          id="exchange-api-key"
          type="text"
          value={apiKey}
          onChange={(event) => setApiKey(event.target.value)}
          autoComplete="off"
          required
        />
      </div>
      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="exchange-api-secret">API secret</label>
        <div className="input-with-action">
          <input
            id="exchange-api-secret"
            type={showSecret ? 'text' : 'password'}
            value={apiSecret}
            onChange={(event) => setApiSecret(event.target.value)}
            autoComplete="new-password"
            required
          />
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setShowSecret((value) => !value)}
          >
            {showSecret ? 'Hide' : 'Show'}
          </button>
        </div>
        <p className="text-xs text-muted mt-1">The secret is encrypted in the browser before it is sent.</p>
      </div>
      {error && <div className="form-error" role="alert">{error}</div>}
      <div className="flex gap-2 justify-end">
        <button type="button" className="btn btn-secondary" onClick={() => onOpenChange(false)}>
          Cancel
        </button>
        <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
          Connect account
        </button>
      </div>
    </form>
  );
}

export function ConnectAccountDialog({ open, onOpenChange, onConnected, account, accounts }: ConnectAccountDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content className="dialog-content">
          <Dialog.Title className="text-xl font-semibold mb-2">Connect account</Dialog.Title>
          <Dialog.Description className="text-sm text-muted mb-4">
            Connect an exchange simulator account with its API key and secret.
          </Dialog.Description>
          <ConnectAccountForm
            onOpenChange={onOpenChange}
            onConnected={onConnected}
            account={account}
            accounts={accounts}
          />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
