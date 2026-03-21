import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import i18n from '@/i18n';
import { tenantsApi } from './tenants.api';
import { queryKeys } from '@/lib/query/keys';

export function useTenants(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.tenants.lists(),
    queryFn: () => tenantsApi.getTenants(params),
  });
}

export function useTenant(id: string) {
  return useQuery({
    queryKey: queryKeys.tenants.detail(id),
    queryFn: () => tenantsApi.getTenantById(id),
    enabled: !!id,
  });
}

export function useActivateTenant() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => tenantsApi.activateTenant(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.all });
      toast.success(i18n.t('tenants.tenantActivated'));
    },
  });
}

export function useSuspendTenant() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => tenantsApi.suspendTenant(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.all });
      toast.success(i18n.t('tenants.tenantSuspended'));
    },
  });
}

export function useDeactivateTenant() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => tenantsApi.deactivateTenant(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.all });
      toast.success(i18n.t('tenants.tenantDeactivated'));
    },
  });
}
