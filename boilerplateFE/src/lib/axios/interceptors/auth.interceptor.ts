import type { AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { storage } from '@/utils/storage';
import { useUIStore } from '@/stores';

const AUTH_PATHS = ['/Auth/login', '/Auth/register', '/Auth/register-tenant', '/Auth/forgot-password', '/Auth/reset-password', '/Auth/verify-email', '/Auth/send-email-verification', '/Auth/refresh-token'];

export const setupAuthInterceptor = (client: AxiosInstance): void => {
  client.interceptors.request.use(
    (config: InternalAxiosRequestConfig) => {
      const token = storage.getAccessToken();
      if (token && config.headers) {
        config.headers.Authorization = `Bearer ${token}`;
      }

      // Don't send tenant header on auth endpoints — they must be tenant-agnostic
      const isAuthEndpoint = AUTH_PATHS.some((path) => config.url?.includes(path));
      if (!isAuthEndpoint) {
        const activeTenantId = useUIStore.getState().activeTenantId;
        if (activeTenantId && config.headers) {
          config.headers['X-Tenant-Id'] = activeTenantId;
        }
      }

      return config;
    },
    (error) => Promise.reject(error)
  );
};
