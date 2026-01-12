export interface AdminResponse<T> {
  data: T | null;
  error: AdminError | null;
}

export interface AdminError {
  message: string;
  code: string;
}

export interface AdminTransaction {
  id: string;
  vaultAccountId: string;
  assetId: string;
  sourceType: 'EXTERNAL' | 'INTERNAL';
  sourceAddress?: string;
  sourceVaultAccountId?: string;
  sourceVaultAccountName?: string;
  destinationType: 'EXTERNAL' | 'INTERNAL';
  destinationVaultAccountId?: string;
  destinationVaultAccountName?: string;
  amount: string;
  destinationAddress: string;
  destinationTag?: string;
  state: TransactionState;
  hash?: string;
  fee: string;
  networkFee: string;
  isFrozen: boolean;
  failureReason?: string;
  replacedByTxId?: string;
  confirmations: number;
  createdAt: string;
  updatedAt: string;
}

export type TransactionState =
  | 'SUBMITTED'
  | 'PENDING_SIGNATURE'
  | 'PENDING_AUTHORIZATION'
  | 'QUEUED'
  | 'BROADCASTING'
  | 'CONFIRMING'
  | 'COMPLETED'
  | 'FAILED'
  | 'REJECTED'
  | 'CANCELLED'
  | 'TIMEOUT';

export interface CreateTransactionRequest {
  assetId: string;
  sourceType: 'EXTERNAL' | 'INTERNAL';
  sourceAddress?: string;
  sourceVaultAccountId?: string;
  destinationType: 'EXTERNAL' | 'INTERNAL';
  destinationAddress?: string;
  destinationVaultAccountId?: string;
  amount: string;
  destinationTag?: string;
  initialState?: string;
}

export interface FailTransactionRequest {
  reason: string;
}

export interface AdminVault {
  id: string;
  name: string;
  hiddenOnUI: boolean;
  customerRefId?: string;
  autoFuel: boolean;
  wallets: AdminWallet[];
  createdAt: string;
  updatedAt: string;
}

export interface AdminWallet {
  assetId: string;
  balance: string;
  lockedAmount: string;
  available: string;
  addressCount: number;
  depositAddress?: string;
}

export interface FrozenBalance {
  assetId: string;
  amount: string;
}

export interface CreateVaultRequest {
  name: string;
  customerRefId?: string;
  autoFuel?: boolean;
}

export interface CreateWalletRequest {
  assetId: string;
}

export interface AdminAutoTransitionSettings {
  enabled: boolean;
}

export interface Asset {
  id: string;
  name: string;
  symbol: string;
  decimals: number;
  type?: string;
}

export interface AdminApiKey {
  id: string;
  name: string;
  key: string;
  createdAt: string;
}

export interface AdminWorkspace {
  id: string;
  name: string;
  apiKeys: AdminApiKey[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateWorkspaceRequest {
  name: string;
}
