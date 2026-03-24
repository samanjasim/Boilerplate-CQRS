import axios from 'axios';
import { apiClient } from '@/lib/axios';
import { API_CONFIG, API_ENDPOINTS } from '@/config';
import type { Tenant, TenantBranding } from '@/types';

export const tenantsApi = {
  getTenants: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.TENANTS.LIST, { params }).then((r) => r.data),

  getTenantById: async (id: string): Promise<Tenant> => {
    const response = await apiClient.get<{ data: Tenant }>(API_ENDPOINTS.TENANTS.DETAIL(id));
    return response.data.data;
  },

  activateTenant: (id: string) =>
    apiClient.post(API_ENDPOINTS.TENANTS.ACTIVATE(id)).then((r) => r.data),

  suspendTenant: (id: string) =>
    apiClient.post(API_ENDPOINTS.TENANTS.SUSPEND(id)).then((r) => r.data),

  deactivateTenant: (id: string) =>
    apiClient.post(API_ENDPOINTS.TENANTS.DEACTIVATE(id)).then((r) => r.data),

  updateBranding: (id: string, data: Record<string, unknown>) =>
    apiClient.put(API_ENDPOINTS.TENANTS.BRANDING(id), data).then((r) => r.data),

  updateBusinessInfo: (id: string, data: Record<string, unknown>) =>
    apiClient.put(API_ENDPOINTS.TENANTS.BUSINESS_INFO(id), data).then((r) => r.data),

  updateCustomText: (id: string, data: Record<string, unknown>) =>
    apiClient.put(API_ENDPOINTS.TENANTS.CUSTOM_TEXT(id), data).then((r) => r.data),

  getBranding: async (slug?: string): Promise<TenantBranding> => {
    const publicClient = axios.create({
      baseURL: API_CONFIG.BASE_URL,
      timeout: API_CONFIG.TIMEOUT,
    });
    const params = slug ? { slug } : {};
    const response = await publicClient.get<{ data: TenantBranding }>(
      API_ENDPOINTS.TENANTS.PUBLIC_BRANDING,
      { params }
    );
    return response.data.data;
  },
};
