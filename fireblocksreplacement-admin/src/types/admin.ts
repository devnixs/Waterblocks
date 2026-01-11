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
  type: 'INCOMING' | 'OUTGOING';
  vaultAccountId: string;
  assetId: string;
  amount: string;
  destinationAddress?: string;
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
