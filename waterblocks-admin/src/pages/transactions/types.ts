import type { Dispatch, SetStateAction } from 'react';

export type TransactionEndpointType = 'VAULT' | 'ONE_TIME' | 'EXTERNAL_RANDOM';

export type SetState<T> = Dispatch<SetStateAction<T>>;
