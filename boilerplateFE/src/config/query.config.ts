import type { QueryClientConfig } from '@tanstack/react-query';
import type { AxiosError } from 'axios';

export const queryClientConfig: QueryClientConfig = {
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, // 5 minutes
      gcTime: 10 * 60 * 1000, // 10 minutes
      retry: (failureCount, error) => {
        const status = (error as AxiosError)?.response?.status;
        if (status === 401 || status === 403) return false;
        return failureCount < 1;
      },
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 0,
    },
  },
};
