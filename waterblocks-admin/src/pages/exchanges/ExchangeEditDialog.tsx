import { useState } from 'react';
import type { FormEvent } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { useAssets, useSaveExchange } from '../../api/queries';
import { useToast } from '../../components/ToastProvider';
import type { AdminExchange, AdminExchangeCurrency } from '../../types/admin';

type ExchangeEditDialogProps = {
  open: boolean;
  exchange?: AdminExchange;
  onClose: () => void;
};

type ExchangeFormProps = {
  exchange?: AdminExchange;
  onClose: () => void;
};

function ExchangeForm({ exchange, onClose }: ExchangeFormProps) {
  const { data: assets } = useAssets();
  const { showToast } = useToast();
  const save = useSaveExchange(exchange?.exchangeType);
  const [exchangeType, setExchangeType] = useState(exchange?.exchangeType ?? '');
  const [name, setName] = useState(exchange?.name ?? '');
  const [simulatorBaseUrl, setSimulatorBaseUrl] = useState(exchange?.simulatorBaseUrl ?? '');
  const [oneAccountPerWorkspace, setOneAccountPerWorkspace] = useState(exchange?.oneAccountPerWorkspace ?? false);
  const [currencies, setCurrencies] = useState<AdminExchangeCurrency[]>(exchange?.currencies ?? []);
  const [error, setError] = useState('');

  const updateCurrency = (index: number, change: Partial<AdminExchangeCurrency>) => {
    setCurrencies((rows) => rows.map((row, rowIndex) => (rowIndex === index ? { ...row, ...change } : row)));
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    try {
      const response = await save.mutateAsync({
        exchangeType: exchange ? undefined : exchangeType.trim().toUpperCase(),
        name: name.trim(),
        simulatorBaseUrl: simulatorBaseUrl.trim() || undefined,
        oneAccountPerWorkspace,
        currencies: currencies.map((row) => ({ assetId: row.assetId, currency: row.currency.trim() })),
      });
      if (response.error || !response.data) {
        setError(response.error?.message ?? 'The exchange could not be saved');
        return;
      }

      showToast({ title: `${response.data.name} saved`, type: 'success', duration: 3000 });
      onClose();
    } catch {
      setError('The Waterblocks API cannot be reached');
    }
  };

  return (
    <form onSubmit={handleSubmit} className="grid gap-4">
      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="exchange-type">Exchange type</label>
        <input
          id="exchange-type"
          type="text"
          placeholder="For example DERIBIT"
          value={exchangeType}
          onChange={(event) => setExchangeType(event.target.value)}
          readOnly={!!exchange}
          required
        />
      </div>
      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="exchange-name">Name</label>
        <input id="exchange-name" type="text" value={name} onChange={(event) => setName(event.target.value)} required />
      </div>
      <div>
        <label className="block text-sm text-muted mb-1" htmlFor="exchange-simulator-url">Simulator URL</label>
        <input
          id="exchange-simulator-url"
          type="text"
          placeholder="Empty: accounts of this exchange cannot connect"
          value={simulatorBaseUrl}
          onChange={(event) => setSimulatorBaseUrl(event.target.value)}
        />
      </div>
      <label className="flex items-center gap-2 cursor-pointer">
        <input
          type="checkbox"
          checked={oneAccountPerWorkspace}
          onChange={(event) => setOneAccountPerWorkspace(event.target.checked)}
        />
        <span className="text-sm">One account per workspace</span>
      </label>
      <div className="grid gap-2">
        <span className="text-sm text-muted">Currencies: the Waterblocks asset and its name at the exchange</span>
        {currencies.map((row, index) => (
          <div key={index} className="input-with-action" data-currency-row={index}>
            <select
              aria-label={`Asset ${index + 1}`}
              value={row.assetId}
              onChange={(event) => updateCurrency(index, { assetId: event.target.value })}
              required
            >
              <option value="">Select an asset</option>
              {(assets ?? []).map((asset) => (
                <option key={asset.id} value={asset.id}>{asset.id}</option>
              ))}
            </select>
            <input
              type="text"
              aria-label={`Currency ${index + 1}`}
              placeholder="Exchange currency"
              value={row.currency}
              onChange={(event) => updateCurrency(index, { currency: event.target.value })}
              required
            />
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setCurrencies((rows) => rows.filter((_, rowIndex) => rowIndex !== index))}
            >
              Remove
            </button>
          </div>
        ))}
        <div>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setCurrencies((rows) => [...rows, { assetId: '', currency: '' }])}
          >
            Add currency
          </button>
        </div>
      </div>
      {error && <div className="form-error" role="alert">{error}</div>}
      <div className="flex gap-2 justify-end">
        <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={save.isPending}>Save exchange</button>
      </div>
    </form>
  );
}

export function ExchangeEditDialog({ open, exchange, onClose }: ExchangeEditDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content className="dialog-content">
          <Dialog.Title className="text-xl font-semibold mb-2">{exchange ? `Edit ${exchange.name}` : 'Add exchange'}</Dialog.Title>
          <Dialog.Description className="text-sm text-muted mb-4">
            An exchange is data. Its accounts connect to the simulator at this URL.
          </Dialog.Description>
          {open && <ExchangeForm exchange={exchange} onClose={onClose} />}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
