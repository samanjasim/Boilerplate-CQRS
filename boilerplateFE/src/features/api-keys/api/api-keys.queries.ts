import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { apiKeysApi } from './api-keys.api';
import { queryKeys } from '@/lib/query/keys';
import i18n from '@/i18n';
import type { CreateApiKeyData, UpdateApiKeyData } from './api-keys.api';

export function useApiKeys(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.apiKeys.list(params),
    queryFn: () => apiKeysApi.getApiKeys(params),
  });
}

export function useApiKey(id: string) {
  return useQuery({
    queryKey: queryKeys.apiKeys.detail(id),
    queryFn: () => apiKeysApi.getApiKeyById(id),
    enabled: !!id,
  });
}

export function useCreateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateApiKeyData) => apiKeysApi.createApiKey(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.createdSuccess'));
    },
  });
}

export function useUpdateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateApiKeyData }) =>
      apiKeysApi.updateApiKey(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.updatedSuccess'));
    },
  });
}

export function useRevokeApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiKeysApi.revokeApiKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.revokedSuccess'));
    },
  });
}

export function useEmergencyRevokeApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      apiKeysApi.emergencyRevokeApiKey(id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.emergencyRevokedSuccess'));
    },
  });
}
