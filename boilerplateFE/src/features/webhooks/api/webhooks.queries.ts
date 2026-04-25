import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { webhooksApi } from './webhooks.api';
import type { CreateWebhookData, UpdateWebhookData } from '@/types';
import { toast } from 'sonner';
import i18n from '@/i18n';

// ── Queries ────────────────────────────────────────────────────────────────

export function useWebhookEndpoints(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.webhooks.endpoints.list(),
    queryFn: () => webhooksApi.getEndpoints(params),
  });
}

export function useWebhookEndpoint(id: string) {
  return useQuery({
    queryKey: queryKeys.webhooks.endpoints.detail(id),
    queryFn: () => webhooksApi.getEndpointById(id),
    enabled: !!id,
  });
}

export function useWebhookDeliveries(id: string, params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.webhooks.deliveries.list(id, params),
    queryFn: () => webhooksApi.getDeliveries(id, params),
    enabled: !!id,
  });
}

export function useWebhookEventTypes() {
  return useQuery({
    queryKey: queryKeys.webhooks.eventTypes(),
    queryFn: () => webhooksApi.getEventTypes(),
  });
}

// ── Mutations ──────────────────────────────────────────────────────────────

export function useCreateWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateWebhookData) => webhooksApi.createEndpoint(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.endpoints.all });
      toast.success(i18n.t('webhooks.endpointCreated'));
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function useUpdateWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateWebhookData) => webhooksApi.updateEndpoint(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.endpoints.all });
      toast.success(i18n.t('webhooks.endpointUpdated'));
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function useDeleteWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => webhooksApi.deleteEndpoint(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.endpoints.all });
      toast.success(i18n.t('webhooks.endpointDeleted'));
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function useTestWebhook() {
  return useMutation({
    mutationFn: (id: string) => webhooksApi.testEndpoint(id),
    onSuccess: () => {
      toast.success(i18n.t('webhooks.testSent'));
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function useRedeliverWebhook() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (deliveryId: string) => webhooksApi.redeliverDelivery(deliveryId),
    onSuccess: () => {
      // The redeliver endpoint returns 200 the moment the message is queued, but
      // the consumer creates the new delivery row asynchronously. Invalidating
      // immediately would refetch before the row exists. Invalidate now (best
      // effort) and again after a short delay to catch the typical consumer
      // round-trip — a single timeout covers ~95% of real-world latencies.
      queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.deliveries.all });
      window.setTimeout(() => {
        queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.deliveries.all });
      }, 2500);
      toast.success(i18n.t('webhooks.redeliverQueued'));
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

export function useRegenerateWebhookSecret() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => webhooksApi.regenerateSecret(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.webhooks.endpoints.all });
      toast.success(i18n.t('webhooks.secretRegenerated'));
    },
    // onError is handled by the global axios error interceptor (error.interceptor.ts)
  });
}

// ── Admin Queries ─────────────────────────────────────────────────────────

export function useWebhookAdminEndpoints(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: ['webhooks', 'admin', 'endpoints', params],
    queryFn: () => webhooksApi.getAdminEndpoints(params),
  });
}

export function useWebhookAdminStats() {
  return useQuery({
    queryKey: ['webhooks', 'admin', 'stats'],
    queryFn: () => webhooksApi.getAdminStats(),
  });
}

export function useWebhookAdminDeliveries(endpointId: string, params?: Record<string, unknown>) {
  return useQuery({
    queryKey: ['webhooks', 'admin', 'deliveries', endpointId, params],
    queryFn: () => webhooksApi.getAdminDeliveries(endpointId, params),
    enabled: !!endpointId,
  });
}
