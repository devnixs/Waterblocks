import type { Dispatch, SetStateAction } from 'react';

export type TransactionEndpointType = 'VAULT' | 'ONE_TIME' | 'EXTERNAL_RANDOM' | 'SOURCELESS_EXCHANGE';

export type SetState<T> = Dispatch<SetStateAction<T>>;
