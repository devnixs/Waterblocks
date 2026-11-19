import type { AdminExchangeAccount, AdminExchangeAsset } from '../../types/admin';

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

export function collateralAccountName(account: AdminExchangeAccount): string {
  return `${account.name} (Collateral)`;
}

export function trimAmount(value: string | undefined): string {
  if (!value) return '';
  return value.includes('.') ? value.replace(/\.?0+$/, '') : value;
}
