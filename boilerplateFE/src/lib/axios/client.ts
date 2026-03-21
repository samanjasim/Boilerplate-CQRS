import axios from 'axios';
import { API_CONFIG } from '@/config';
import {
  setupAuthInterceptor,
  setupRefreshInterceptor,
  setupErrorInterceptor,
} from './interceptors';

const apiClient = axios.create({
  baseURL: API_CONFIG.BASE_URL,
  timeout: API_CONFIG.TIMEOUT,
  headers: {
    'Content-Type': 'application/json',
  },
});

setupAuthInterceptor(apiClient);
setupRefreshInterceptor(apiClient);
setupErrorInterceptor(apiClient);

export { apiClient };
