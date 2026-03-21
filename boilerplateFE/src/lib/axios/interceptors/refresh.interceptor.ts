import type { AxiosInstance, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { storage } from '@/utils/storage';
import { useAuthStore } from '@/stores';
import { API_CONFIG, API_ENDPOINTS } from '@/config';
import axios from 'axios';

interface FailedRequest {
  resolve: (token: string) => void;
  reject: (error: Error) => void;
}

let isRefreshing = false;
let failedRequestsQueue: FailedRequest[] = [];

const AUTH_ENDPOINTS = [
  API_ENDPOINTS.AUTH.LOGIN,
  API_ENDPOINTS.AUTH.REGISTER,
  API_ENDPOINTS.AUTH.REFRESH_TOKEN,
];

const isAuthEndpoint = (url?: string): boolean => {
  if (!url) return false;
  return AUTH_ENDPOINTS.some((endpoint) => url.includes(endpoint));
};

const handleAuthFailure = () => {
  storage.clearTokens();
  useAuthStore.getState().logout();
};

const processQueue = (error: Error | null, token: string | null = null) => {
  failedRequestsQueue.forEach(({ resolve, reject }) => {
    if (error) {
      reject(error);
    } else if (token) {
      resolve(token);
    }
  });
  failedRequestsQueue = [];
};

export const setupRefreshInterceptor = (client: AxiosInstance): void => {
  client.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

      // Don't refresh for non-401 errors, already-retried requests, or auth endpoints
      if (
        error.response?.status !== 401 ||
        originalRequest._retry ||
        isAuthEndpoint(originalRequest.url)
      ) {
        return Promise.reject(error);
      }

      if (isRefreshing) {
        return new Promise<string>((resolve, reject) => {
          failedRequestsQueue.push({ resolve, reject });
        })
          .then((token) => {
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${token}`;
            }
            return client(originalRequest);
          })
          .catch((err) => Promise.reject(err));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const refreshToken = storage.getRefreshToken();

      if (!refreshToken) {
        isRefreshing = false;
        handleAuthFailure();
        return Promise.reject(error);
      }

      try {
        const response = await axios.post(
          `${API_CONFIG.BASE_URL}${API_ENDPOINTS.AUTH.REFRESH_TOKEN}`,
          { refreshToken },
          { headers: { 'Content-Type': 'application/json' } }
        );

        const { accessToken: newAccessToken, refreshToken: newRefreshToken } = response.data.data;
        storage.setTokens(newAccessToken, newRefreshToken);

        processQueue(null, newAccessToken);

        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        }
        return client(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError as Error);
        handleAuthFailure();
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }
  );
};
