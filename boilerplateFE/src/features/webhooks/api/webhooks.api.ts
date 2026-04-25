import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  WebhookEndpoint,
  WebhookDelivery,
  WebhookEventType,
  WebhookAdminStats,
  CreateWebhookData,
  UpdateWebhookData,
  CreateWebhookResponse,
  PaginatedResponse,
} from '@/types';

export const webhooksApi = {
  getEndpoints: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.WEBHOOKS.LIST, { params }).then(r => r.data),

  getEndpointById: (id: string) =>
    apiClient.get<{ data: WebhookEndpoint }>(API_ENDPOINTS.WEBHOOKS.DETAIL(id)).then(r => r.data.data),

  createEndpoint: (data: CreateWebhookData) =>
    apiClient.post<{ data: CreateWebhookResponse }>(API_ENDPOINTS.WEBHOOKS.LIST, data).then(r => r.data.data),

  updateEndpoint: (data: UpdateWebhookData) =>
    apiClient.put(API_ENDPOINTS.WEBHOOKS.DETAIL(data.id), data).then(r => r.data),

  deleteEndpoint: (id: string) =>
    apiClient.delete(API_ENDPOINTS.WEBHOOKS.DETAIL(id)).then(r => r.data),

  getDeliveries: (id: string, params?: Record<string, unknown>) =>
    apiClient
      .get<PaginatedResponse<WebhookDelivery>>(API_ENDPOINTS.WEBHOOKS.DELIVERIES(id), { params })
      .then(r => r.data),

  testEndpoint: (id: string) =>
    apiClient.post(API_ENDPOINTS.WEBHOOKS.TEST(id)).then(r => r.data),

  redeliverDelivery: (deliveryId: string) =>
    apiClient.post(API_ENDPOINTS.WEBHOOKS.REDELIVER(deliveryId)).then(r => r.data),

  regenerateSecret: (id: string) =>
    apiClient
      .post<{ data: string }>(API_ENDPOINTS.WEBHOOKS.REGENERATE_SECRET(id))
      .then(r => r.data.data),

  getEventTypes: () =>
    apiClient.get<{ data: WebhookEventType[] }>(API_ENDPOINTS.WEBHOOKS.EVENTS).then(r => r.data.data),

  // Platform admin
  getAdminEndpoints: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.WEBHOOKS.ADMIN, { params }).then(r => r.data),

  getAdminStats: () =>
    apiClient.get<{ data: WebhookAdminStats }>(API_ENDPOINTS.WEBHOOKS.ADMIN_STATS).then(r => r.data.data),

  getAdminDeliveries: (endpointId: string, params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.WEBHOOKS.ADMIN_DELIVERIES(endpointId), { params }).then(r => r.data),
};
