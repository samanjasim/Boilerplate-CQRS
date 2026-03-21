import { QueryClient } from '@tanstack/react-query';
import { queryClientConfig } from '@/config';

export const queryClient = new QueryClient(queryClientConfig);
