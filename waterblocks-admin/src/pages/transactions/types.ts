import type { Dispatch, SetStateAction } from 'react';

export type TransactionEndpointType = 'VAULT' | 'ONE_TIME';

export type SetState<T> = Dispatch<SetStateAction<T>>;
