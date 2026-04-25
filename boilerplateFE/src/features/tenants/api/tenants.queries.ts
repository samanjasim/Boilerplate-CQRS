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

export function useUpdateTenantBranding() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Record<string, unknown> }) =>
      tenantsApi.updateBranding(id, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.detail(variables.id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.lists() });
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.all });
      toast.success(i18n.t('tenants.brandingSaved'));
    },
  });
}

export function useUpdateTenantBusinessInfo() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Record<string, unknown> }) =>
      tenantsApi.updateBusinessInfo(id, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.detail(variables.id) });
      toast.success(i18n.t('tenants.businessInfoSaved'));
    },
  });
}

export function useUpdateTenantCustomText() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Record<string, unknown> }) =>
      tenantsApi.updateCustomText(id, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.detail(variables.id) });
      toast.success(i18n.t('tenants.customTextSaved'));
    },
  });
}

export function useSetTenantDefaultRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, roleId }: { id: string; roleId: string | null }) =>
      tenantsApi.setDefaultRole(id, roleId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.detail(variables.id) });
      toast.success(i18n.t('tenants.defaultRoleUpdated'));
    },
  });
}

export function useTenantBranding(slug?: string) {
  return useQuery({
    queryKey: [...queryKeys.tenants.all, 'branding', slug ?? 'default'],
    queryFn: () => tenantsApi.getBranding(slug),
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Mark the current tenant as onboarded (or clear the flag to re-trigger
 * the wizard). Invalidates the current-user query so `tenantOnboardedAt`
 * refreshes and the wizard hides without a page reload.
 */
export function useMarkTenantOnboarded() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, onboarded }: { id: string; onboarded: boolean }) =>
      tenantsApi.markOnboarded(id, onboarded),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() });
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.detail(variables.id) });
    },
  });
}
