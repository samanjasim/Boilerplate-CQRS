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
  });
}

export function useTestWebhook() {
  return useMutation({
    mutationFn: (id: string) => webhooksApi.testEndpoint(id),
    onSuccess: () => {
      toast.success(i18n.t('webhooks.testSent'));
    },
  });
}
