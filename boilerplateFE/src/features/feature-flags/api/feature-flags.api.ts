import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';

export interface FeatureFlagDto {
  id: string;
  key: string;
  name: string;
  description: string | null;
  defaultValue: string;
  valueType: 'Boolean' | 'String' | 'Integer' | 'Json';
  category: string | null;
  isSystem: boolean;
  tenantOverrideValue: string | null;
  resolvedValue: string;
  createdAt: string;
  modifiedAt: string | null;
}

export interface CreateFeatureFlagData {
  key: string;
  name: string;
  description?: string | null;
  defaultValue: string;
  valueType: number;
  category?: string | null;
  isSystem: boolean;
}

export interface UpdateFeatureFlagData {
  id: string;
  name: string;
  description?: string | null;
  defaultValue: string;
  category?: string | null;
}

export interface SetTenantOverrideData {
  value: string;
}

export const featureFlagsApi = {
  getAll: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.FEATURE_FLAGS.LIST, { params }).then(r => r.data),

  getByKey: (key: string) =>
    apiClient.get<{ data: FeatureFlagDto }>(API_ENDPOINTS.FEATURE_FLAGS.BY_KEY(key)).then(r => r.data.data),

  create: (data: CreateFeatureFlagData) =>
    apiClient.post(API_ENDPOINTS.FEATURE_FLAGS.LIST, data).then(r => r.data),

  update: (data: UpdateFeatureFlagData) =>
    apiClient.put(API_ENDPOINTS.FEATURE_FLAGS.DETAIL(data.id), data).then(r => r.data),

  delete: (id: string) =>
    apiClient.delete(API_ENDPOINTS.FEATURE_FLAGS.DETAIL(id)).then(r => r.data),

  setTenantOverride: (flagId: string, tenantId: string, data: SetTenantOverrideData) =>
    apiClient.put(API_ENDPOINTS.FEATURE_FLAGS.TENANT_OVERRIDE(flagId, tenantId), data).then(r => r.data),

  removeTenantOverride: (flagId: string, tenantId: string) =>
    apiClient.delete(API_ENDPOINTS.FEATURE_FLAGS.TENANT_OVERRIDE(flagId, tenantId)).then(r => r.data),
};
