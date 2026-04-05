import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';

export const dashboardApi = {
  getAnalytics: (period: string = '30d') =>
    apiClient.get(API_ENDPOINTS.DASHBOARD.ANALYTICS, { params: { period } }).then(r => r.data),
};
