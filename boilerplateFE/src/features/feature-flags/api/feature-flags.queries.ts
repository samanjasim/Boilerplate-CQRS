import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { featureFlagsApi } from './feature-flags.api';
import type { CreateFeatureFlagData, UpdateFeatureFlagData, SetTenantOverrideData } from './feature-flags.api';
import { toast } from 'sonner';
import i18n from '@/i18n';

export function useFeatureFlags(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.featureFlags.list(params),
    queryFn: () => featureFlagsApi.getAll(params),
  });
}

export function useFeatureFlagByKey(key: string) {
  return useQuery({
    queryKey: queryKeys.featureFlags.detail(key),
    queryFn: () => featureFlagsApi.getByKey(key),
    enabled: !!key,
  });
}

export function useCreateFeatureFlag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateFeatureFlagData) => featureFlagsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.createdSuccess'));
    },
  });
}

export function useUpdateFeatureFlag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateFeatureFlagData) => featureFlagsApi.update(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.updatedSuccess'));
    },
  });
}

export function useDeleteFeatureFlag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => featureFlagsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.deletedSuccess'));
    },
  });
}

export function useSetTenantOverride() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ flagId, tenantId, data }: { flagId: string; tenantId: string; data: SetTenantOverrideData }) =>
      featureFlagsApi.setTenantOverride(flagId, tenantId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.overrideSet'));
    },
  });
}

export function useRemoveTenantOverride() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ flagId, tenantId }: { flagId: string; tenantId: string }) =>
      featureFlagsApi.removeTenantOverride(flagId, tenantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.overrideRemoved'));
    },
  });
}

export function useOptOutFeatureFlag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (flagId: string) => featureFlagsApi.optOut(flagId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.optOutSuccess'));
    },
  });
}

export function useRemoveOptOut() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (flagId: string) => featureFlagsApi.removeOptOut(flagId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.optInSuccess'));
    },
  });
}
