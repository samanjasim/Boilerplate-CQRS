import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { billingApi } from './billing.api';
import type { CreatePlanData, UpdatePlanData, ChangePlanData } from '@/types';
import { toast } from 'sonner';
import i18n from '@/i18n';

// ── Queries ────────────────────────────────────────────────────────────────

export function usePlans(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.plans.list(params),
    queryFn: () => billingApi.getPlans(params),
  });
}

export function useAllPlans(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.plans.list({ ...params, _all: true }),
    queryFn: () => billingApi.getAllPlans(params),
  });
}

export function usePlan(id: string) {
  return useQuery({
    queryKey: queryKeys.billing.plans.detail(id),
    queryFn: () => billingApi.getPlanById(id),
    enabled: !!id,
  });
}

export function useSubscription() {
  return useQuery({
    queryKey: queryKeys.billing.subscription.current(),
    queryFn: () => billingApi.getSubscription(),
  });
}

export function useUsage() {
  return useQuery({
    queryKey: queryKeys.billing.usage.current(),
    queryFn: () => billingApi.getUsage(),
  });
}

export function usePayments(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.payments.list(params),
    queryFn: () => billingApi.getPayments(params),
  });
}

export function usePlanOptions() {
  return useQuery({
    queryKey: ['billing', 'plan-options'],
    queryFn: () => billingApi.getPlanOptions(),
  });
}

// ── Mutations ──────────────────────────────────────────────────────────────

export function useCreatePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePlanData) => billingApi.createPlan(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.billing.plans.all });
      toast.success(i18n.t('billing.planCreated'));
    },
  });
}

export function useUpdatePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdatePlanData) => billingApi.updatePlan(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.billing.plans.all });
      toast.success(i18n.t('billing.planUpdated'));
    },
  });
}

export function useDeactivatePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => billingApi.deactivatePlan(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.billing.plans.all });
      toast.success(i18n.t('billing.planDeactivated'));
    },
  });
}

export function useChangePlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ChangePlanData) => billingApi.changePlan(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.billing.subscription.all });
      toast.success(i18n.t('billing.planChanged'));
    },
  });
}

export function useCancelSubscription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => billingApi.cancelSubscription(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.billing.subscription.all });
      toast.success(i18n.t('billing.subscriptionCanceled'));
    },
  });
}

export function useResyncPlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => billingApi.resyncPlan(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.billing.plans.all });
      toast.success(i18n.t('billing.planResynced'));
    },
  });
}

// ── Platform Admin: Subscriptions ─────────────────────────────────────────

export function useAllSubscriptions(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.subscriptions.list(params),
    queryFn: () => billingApi.getAllSubscriptions(params),
  });
}

export function useTenantUsage(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.billing.usage.tenant(tenantId),
    queryFn: () => billingApi.getTenantUsage(tenantId),
    enabled: !!tenantId,
  });
}

export function useTenantPayments(tenantId: string, params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.payments.tenant(tenantId, params),
    queryFn: () => billingApi.getTenantPayments(tenantId, params),
    enabled: !!tenantId,
  });
}

export function useTenantSubscription(tenantId: string) {
  return useQuery({
    queryKey: ['billing', 'subscription', 'tenant', tenantId],
    queryFn: () => billingApi.getTenantSubscription(tenantId),
    enabled: !!tenantId,
  });
}

export function useChangeTenantPlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ tenantId, data }: { tenantId: string; data: ChangePlanData }) =>
      billingApi.changeTenantPlan(tenantId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.billing.subscriptions.all });
      toast.success(i18n.t('billing.planChanged'));
    },
  });
}
