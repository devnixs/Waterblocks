import type { AdminExchangeAccount, AdminExchangeAsset, AdminExchangeProvider } from '../../types/admin';

export function formatExchangeAmount(value: string | undefined): string {
  const parsed = parseFloat(value ?? '0');
  return Number.isNaN(parsed) ? '-' : parsed.toFixed(8);
}

export function findExchangeAsset(account: AdminExchangeAccount, assetId: string): AdminExchangeAsset | undefined {
  return account.assets.find((asset) => asset.assetId === assetId);
}

export function createEmptyExchangeAsset(assetId: string): AdminExchangeAsset {
  return {
    assetId,
    total: '0',
    available: '0',
    credit: '0',
    lockedAmount: '0',
    locked: '0',
    exchangeHold: '0',
  };
}

/**
 * Returns the rows of the asset table: the stored assets plus a zero row for each asset that a transfer can use.
 */
export function listAccountAssets(account: AdminExchangeAccount, transferAssetIds: string[]): AdminExchangeAsset[] {
  const missingAssets = transferAssetIds
    .filter((assetId, index) => transferAssetIds.indexOf(assetId) === index && !findExchangeAsset(account, assetId))
    .map(createEmptyExchangeAsset);
  return [...account.assets, ...missingAssets].sort((left, right) => left.assetId.localeCompare(right.assetId));
}

export function collateralAccountName(account: AdminExchangeAccount): string {
  return `${account.name} (Collateral)`;
}

export function trimAmount(value: string | undefined): string {
  if (!value) return '';
  return value.includes('.') ? value.replace(/\.?0+$/, '') : value;
}

export function findProvider(
  providers: AdminExchangeProvider[],
  exchangeType: string,
): AdminExchangeProvider | undefined {
  return providers.find((provider) => provider.exchangeType === exchangeType);
}

/**
 * Returns the exchange name of a provider. An exchange type with no simulator shows its type.
 */
export function exchangeName(providers: AdminExchangeProvider[], exchangeType: string): string {
  return findProvider(providers, exchangeType)?.name ?? exchangeType;
}

/**
 * Returns the providers that allow one account in a workspace and that already have an account in it.
 */
export function findFullProviders(
  providers: AdminExchangeProvider[],
  accounts: AdminExchangeAccount[],
): AdminExchangeProvider[] {
  return providers.filter((provider) => (
    provider.oneAccountPerWorkspace && accounts.some((account) => account.exchangeType === provider.exchangeType)
  ));
}

export function oneAccountMessage(providers: AdminExchangeProvider[]): string {
  const names = providers.map((provider) => provider.name);
  return names.length === 1
    ? `A workspace can have only one ${names[0]} account.`
    : `A workspace can have only one account of each of these exchanges: ${names.join(', ')}.`;
}

/**
 * Returns the collateral totals of an account as text, for example "1.5 BTC, 200 USDT".
 */
export function collateralSummary(account: AdminExchangeAccount): string {
  const totals = account.assets
    .filter((asset) => parseFloat(asset.total) > 0)
    .map((asset) => `${trimAmount(asset.total)} ${asset.assetId}`);
  return totals.length > 0 ? totals.join(', ') : '-';
}
