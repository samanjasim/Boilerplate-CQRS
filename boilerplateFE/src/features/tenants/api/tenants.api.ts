import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { Tenant } from '@/types';

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
};
