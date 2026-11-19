import { useState } from 'react';
import type { FormEvent } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../../api/adminClient';
import type { AdminExchangeAccount } from '../../types/admin';

type ExchangeProvider = {
  exchangeType: string;
  name: string;
  label: string;
};

const EXCHANGE_PROVIDERS: ExchangeProvider[] = [
  { exchangeType: 'GATEIO', name: 'Gate', label: 'Off Exchange' },
];

type ConnectAccountDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConnected: (account: AdminExchangeAccount) => void;
  account?: AdminExchangeAccount;
};

type ConnectAccountFormProps = Omit<ConnectAccountDialogProps, 'open'>;

type ProviderStepProps = {
  onSelect: (provider: ExchangeProvider) => void;
  onCancel: () => void;
};

function ProviderStep({ onSelect, onCancel }: ProviderStepProps) {
  return (
    <div className="grid gap-4">
      <div className="grid gap-2">
        {EXCHANGE_PROVIDERS.map((provider) => (
          <button
            key={provider.exchangeType}
            type="button"
            className="exchange-provider"
            onClick={() => onSelect(provider)}
          >
            <span className="font-medium">{provider.name}</span>
            <span className="text-sm text-muted">{provider.label}</span>
          </button>
        ))}
      </div>
      <div className="flex justify-end">
        <button type="button" className="btn btn-secondary" onClick={onCancel}>Cancel</button>
      </div>
    </div>
  );
}

function ConnectAccountForm({ onOpenChange, onConnected, account }: ConnectAccountFormProps) {
  const queryClient = useQueryClient();
  const initialProvider = account
    ? EXCHANGE_PROVIDERS.find((provider) => provider.exchangeType === account.exchangeType) ?? null
    : null;
  const [provider, setProvider] = useState<ExchangeProvider | null>(initialProvider);
  const [displayName, setDisplayName] = useState(account?.name ?? '');
  const [apiKey, setApiKey] = useState('');
  const [apiSecret, setApiSecret] = useState('');
  const [showSecret, setShowSecret] = useState(false);
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!provider) {
    return <ProviderStep onSelect={setProvider} onCancel={() => onOpenChange(false)} />;
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setIsSubmitting(true);
    try {
      const credentials = { apiKey, apiSecret };
      const response = account
        ? await adminApi.connectExchangeAccount(account.id, credentials)
        : await adminApi.createExchangeAccount({
          ...credentials,
          exchangeType: provider.exchangeType,
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
    } catch {
      setError('The Waterblocks API cannot be reached');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="grid gap-4" autoComplete="off">
      <div className="exchange-detail-row">
        <span className="font-medium">{provider.name}</span>
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

export function ConnectAccountDialog({ open, onOpenChange, onConnected, account }: ConnectAccountDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content className="dialog-content">
          <Dialog.Title className="text-xl font-semibold mb-2">Connect account</Dialog.Title>
          <Dialog.Description className="text-sm text-muted mb-4">
            Connect a Gate simulator account with its API key and secret.
          </Dialog.Description>
          <ConnectAccountForm onOpenChange={onOpenChange} onConnected={onConnected} account={account} />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
