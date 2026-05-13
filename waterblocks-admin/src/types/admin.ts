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
  sourceVaultAccountName?: string;
  destinationType: 'EXTERNAL' | 'INTERNAL';
  destinationVaultAccountName?: string;
  amount: string;
  destinationAddress: string;
  destinationTag?: string;
  state: TransactionState;
  hash?: string;
  fee: string;
  networkFee: string;
  feeCurrency?: string;
  treatAsGrossAmount: boolean;
  isFrozen: boolean;
  failureReason?: string;
  replacedByTxId?: string;
  confirmations: number;
  createdAt: string;
  updatedAt: string;
  initiatedBy?: string;
}

export interface AdminTransactionsPage {
  items: AdminTransaction[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
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
  sourceAddress?: string;
  destinationAddress?: string;
  amount: string;
  destinationTag?: string;
  initialState?: string;
  hash?: string;
  networkFee?: string;
  feeLevel?: 'LOW' | 'MEDIUM' | 'HIGH';
  treatAsGrossAmount?: boolean;
  initiatedBy?: string;
}

export interface EstimateFeeRequest {
  assetId: string;
  amount?: string;
  source?: { type: string };
  destination?: { type: string };
}

export interface FeeEstimate {
  feePerByte?: string;
  gasPrice?: string;
  gasLimit?: string;
  networkFee?: string;
  baseFee?: string;
  priorityFee?: string;
}

export interface EstimateFeeResponse {
  low: FeeEstimate;
  medium: FeeEstimate;
  high: FeeEstimate;
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
  isArchived: boolean;
  archivedAt?: string;
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
  addresses: AdminAddress[];
}

export interface AdminAddress {
  id: number;
  addressValue: string;
  tag?: string;
  type: string;
  description?: string;
  addressFormat?: string;
  legacyAddress?: string;
  createdAt: string;
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

export interface UpdateVaultRequest {
  name: string;
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
  contractAddress?: string;
  nativeAsset?: string;
}

export type BlockchainType = 'AccountBased' | 'AddressBased' | 'MemoBased';

export interface AdminAsset {
  id: string;
  name: string;
  symbol: string;
  decimals: number;
  type?: string;
  blockchainType: BlockchainType;
  contractAddress?: string;
  nativeAsset?: string;
  baseFee: number;
  feeAssetId?: string;
  isCaseSensitive: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface CreateAdminAssetRequest {
  assetId: string;
  name: string;
  symbol: string;
  decimals?: number;
  type?: string;
  blockchainType?: BlockchainType;
  contractAddress?: string;
  nativeAsset?: string;
  baseFee?: number;
  feeAssetId?: string;
  isCaseSensitive?: boolean;
  isActive?: boolean;
}

export interface UpdateAdminAssetRequest {
  name?: string;
  symbol?: string;
  decimals?: number;
  type?: string;
  blockchainType?: BlockchainType;
  contractAddress?: string;
  nativeAsset?: string;
  baseFee?: number;
  feeAssetId?: string;
  isCaseSensitive?: boolean;
  isActive?: boolean;
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
  autoTransitionEnabled: boolean;
  apiKeys: AdminApiKey[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateWorkspaceRequest {
  name: string;
  autoTransitionEnabled: boolean;
}

export interface AdminGeneratedAddress {
  address: string;
}
