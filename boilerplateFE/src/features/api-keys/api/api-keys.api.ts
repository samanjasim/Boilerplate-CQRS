import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';

export interface ApiKeyDto {
  id: string;
  name: string;
  keyPrefix: string;
  scopes: string[];
  expiresAt: string | null;
  lastUsedAt: string | null;
  isRevoked: boolean;
  isExpired: boolean;
  isPlatformKey: boolean;
  tenantId: string | null;
  tenantName: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface CreateApiKeyResponse {
  id: string;
  name: string;
  keyPrefix: string;
  fullKey: string;
  scopes: string[];
  expiresAt: string | null;
  createdAt: string;
}

export interface CreateApiKeyData {
  name: string;
  scopes: string[];
  expiresAt?: string | null;
  isPlatformKey?: boolean;
}

export interface UpdateApiKeyData {
  name?: string;
  scopes?: string[];
}

export const apiKeysApi = {
  getApiKeys: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.API_KEYS.LIST, { params }).then(r => r.data),

  getApiKeyById: async (id: string): Promise<ApiKeyDto> => {
    const response = await apiClient.get<{ data: ApiKeyDto }>(API_ENDPOINTS.API_KEYS.DETAIL(id));
    return response.data.data;
  },

  createApiKey: async (data: CreateApiKeyData): Promise<CreateApiKeyResponse> => {
    const response = await apiClient.post<{ data: CreateApiKeyResponse }>(
      API_ENDPOINTS.API_KEYS.LIST,
      data
    );
    return response.data.data;
  },

  updateApiKey: async (id: string, data: UpdateApiKeyData): Promise<ApiKeyDto> => {
    const response = await apiClient.patch<{ data: ApiKeyDto }>(
      API_ENDPOINTS.API_KEYS.DETAIL(id),
      data
    );
    return response.data.data;
  },

  revokeApiKey: async (id: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.API_KEYS.DETAIL(id));
  },

  emergencyRevokeApiKey: async (id: string, reason?: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.API_KEYS.EMERGENCY_REVOKE(id), {
      data: reason ? { reason } : undefined,
    });
  },
};
