import type { AdminExchange, AdminExchangeAccount, AdminExchangeAsset } from '../../types/admin';

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

export function findExchange(exchanges: AdminExchange[], exchangeType: string): AdminExchange | undefined {
  return exchanges.find((exchange) => exchange.exchangeType === exchangeType);
}

/**
 * Returns the name of an exchange from the catalog. An exchange type that is not in the catalog shows its type.
 */
export function exchangeName(exchanges: AdminExchange[], exchangeType: string): string {
  return findExchange(exchanges, exchangeType)?.name ?? exchangeType;
}

/**
 * Returns the exchanges that allow one account in a workspace and that already have an account in it.
 */
export function findFullExchanges(exchanges: AdminExchange[], accounts: AdminExchangeAccount[]): AdminExchange[] {
  return exchanges.filter((exchange) => (
    exchange.oneAccountPerWorkspace && accounts.some((account) => account.exchangeType === exchange.exchangeType)
  ));
}

export function oneAccountMessage(exchanges: AdminExchange[]): string {
  const names = exchanges.map((exchange) => exchange.name);
  return names.length === 1
    ? `A workspace can have only one ${names[0]} account.`
    : `A workspace can have only one account of each of these exchanges: ${names.join(', ')}.`;
}

/**
 * Returns the collateral totals of an account as text, for example "1.5 BTC, 200 USDT_ERC20".
 */
export function collateralSummary(account: AdminExchangeAccount): string {
  const totals = account.assets
    .filter((asset) => parseFloat(asset.total) > 0)
    .map((asset) => `${trimAmount(asset.total)} ${asset.assetId}`);
  return totals.length > 0 ? totals.join(', ') : '-';
}
