import type { Dispatch, SetStateAction } from 'react';

export type TransactionEndpointType = 'EXTERNAL' | 'INTERNAL';

export type SetState<T> = Dispatch<SetStateAction<T>>;
