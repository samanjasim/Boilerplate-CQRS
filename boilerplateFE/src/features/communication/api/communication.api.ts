import { apiClient } from '@/lib/axios/client';
import { API_ENDPOINTS } from '@/config/api.config';
import type {
  ChannelConfigDto,
  ChannelConfigDetailDto,
  AvailableProviderDto,
  CreateChannelConfigData,
  UpdateChannelConfigData,
  TestChannelConfigResponse,
  MessageTemplateDto,
  MessageTemplateDetailDto,
  MessageTemplateOverrideDto,
  CreateTemplateOverrideData,
  UpdateTemplateOverrideData,
  TemplatePreviewDto,
  TriggerRuleDto,
  EventRegistrationDto,
  CreateTriggerRuleData,
  UpdateTriggerRuleData,
  NotificationPreferenceDto,
  NotificationPreferenceItem,
  RequiredNotificationDto,
  IntegrationConfigDto,
  CreateIntegrationConfigData,
  UpdateIntegrationConfigData,
  TestIntegrationConfigResponse,
  DeliveryLogDto,
  DeliveryLogDetailDto,
  DeliveryStatusCountsDto,
  CommunicationDashboardDto,
} from '@/types/communication.types';
import type { NotificationChannel } from '@/types/communication.types';
import type { PaginatedResponse } from '@/types/api.types';

export const communicationApi = {
  // Channel Configs
  getChannelConfigs: () =>
    apiClient.get<{ data: ChannelConfigDto[] }>(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.LIST).then(r => r.data),

  getChannelConfig: (id: string) =>
    apiClient.get<{ data: ChannelConfigDetailDto }>(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.DETAIL(id)).then(r => r.data),

  createChannelConfig: (data: CreateChannelConfigData) =>
    apiClient.post<{ data: ChannelConfigDto }>(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.LIST, data).then(r => r.data),

  updateChannelConfig: (data: UpdateChannelConfigData) =>
    apiClient.put<{ data: ChannelConfigDto }>(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.DETAIL(data.id), data).then(r => r.data),

  deleteChannelConfig: (id: string) =>
    apiClient.delete(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.DETAIL(id)).then(r => r.data),

  testChannelConfig: (id: string) =>
    apiClient.post<{ data: TestChannelConfigResponse }>(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.TEST(id)).then(r => r.data),

  setDefaultChannelConfig: (id: string) =>
    apiClient.post<{ data: ChannelConfigDto }>(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.SET_DEFAULT(id)).then(r => r.data),

  getAvailableProviders: () =>
    apiClient.get<{ data: AvailableProviderDto[] }>(API_ENDPOINTS.COMMUNICATION.CHANNEL_CONFIGS.PROVIDERS).then(r => r.data),

  // Message Templates
  getMessageTemplates: (category?: string) =>
    apiClient.get<{ data: MessageTemplateDto[] }>(API_ENDPOINTS.COMMUNICATION.MESSAGE_TEMPLATES.LIST, {
      params: category ? { category } : undefined,
    }).then(r => r.data),

  getMessageTemplate: (id: string) =>
    apiClient.get<{ data: MessageTemplateDetailDto }>(API_ENDPOINTS.COMMUNICATION.MESSAGE_TEMPLATES.DETAIL(id)).then(r => r.data),

  getTemplateCategories: () =>
    apiClient.get<{ data: string[] }>(API_ENDPOINTS.COMMUNICATION.MESSAGE_TEMPLATES.CATEGORIES).then(r => r.data),

  createTemplateOverride: (id: string, data: CreateTemplateOverrideData) =>
    apiClient.post<{ data: MessageTemplateOverrideDto }>(API_ENDPOINTS.COMMUNICATION.MESSAGE_TEMPLATES.OVERRIDE(id), data).then(r => r.data),

  updateTemplateOverride: (id: string, data: UpdateTemplateOverrideData) =>
    apiClient.put<{ data: MessageTemplateOverrideDto }>(API_ENDPOINTS.COMMUNICATION.MESSAGE_TEMPLATES.OVERRIDE(id), data).then(r => r.data),

  deleteTemplateOverride: (id: string) =>
    apiClient.delete(API_ENDPOINTS.COMMUNICATION.MESSAGE_TEMPLATES.OVERRIDE(id)).then(r => r.data),

  previewTemplate: (id: string, variables?: Record<string, unknown>) =>
    apiClient.post<{ data: TemplatePreviewDto }>(API_ENDPOINTS.COMMUNICATION.MESSAGE_TEMPLATES.PREVIEW(id), { variables }).then(r => r.data),

  // Trigger Rules
  getTriggerRules: () =>
    apiClient.get<{ data: TriggerRuleDto[] }>(API_ENDPOINTS.COMMUNICATION.TRIGGER_RULES.LIST).then(r => r.data),

  getTriggerRule: (id: string) =>
    apiClient.get<{ data: TriggerRuleDto }>(API_ENDPOINTS.COMMUNICATION.TRIGGER_RULES.DETAIL(id)).then(r => r.data),

  createTriggerRule: (data: CreateTriggerRuleData) =>
    apiClient.post<{ data: TriggerRuleDto }>(API_ENDPOINTS.COMMUNICATION.TRIGGER_RULES.LIST, data).then(r => r.data),

  updateTriggerRule: (data: UpdateTriggerRuleData) =>
    apiClient.put<{ data: TriggerRuleDto }>(API_ENDPOINTS.COMMUNICATION.TRIGGER_RULES.DETAIL(data.id), data).then(r => r.data),

  deleteTriggerRule: (id: string) =>
    apiClient.delete(API_ENDPOINTS.COMMUNICATION.TRIGGER_RULES.DETAIL(id)).then(r => r.data),

  toggleTriggerRule: (id: string) =>
    apiClient.post<{ data: TriggerRuleDto }>(API_ENDPOINTS.COMMUNICATION.TRIGGER_RULES.TOGGLE(id)).then(r => r.data),

  // Event Registrations
  getRegisteredEvents: () =>
    apiClient.get<{ data: EventRegistrationDto[] }>(API_ENDPOINTS.COMMUNICATION.EVENT_REGISTRATIONS.LIST).then(r => r.data),

  // Notification Preferences
  getNotificationPreferences: () =>
    apiClient.get<{ data: NotificationPreferenceDto[] }>(API_ENDPOINTS.COMMUNICATION.NOTIFICATION_PREFERENCES.LIST).then(r => r.data),

  updateNotificationPreferences: (preferences: NotificationPreferenceItem[]) =>
    apiClient.put<{ data: NotificationPreferenceDto[] }>(API_ENDPOINTS.COMMUNICATION.NOTIFICATION_PREFERENCES.LIST, { preferences }).then(r => r.data),

  // Required Notifications
  getRequiredNotifications: () =>
    apiClient.get<{ data: RequiredNotificationDto[] }>(API_ENDPOINTS.COMMUNICATION.REQUIRED_NOTIFICATIONS.LIST).then(r => r.data),

  setRequiredNotification: (data: { category: string; channel: NotificationChannel }) =>
    apiClient.post<{ data: RequiredNotificationDto }>(API_ENDPOINTS.COMMUNICATION.REQUIRED_NOTIFICATIONS.LIST, data).then(r => r.data),

  removeRequiredNotification: (id: string) =>
    apiClient.delete(API_ENDPOINTS.COMMUNICATION.REQUIRED_NOTIFICATIONS.DETAIL(id)).then(r => r.data),

  // Integration Configs
  getIntegrationConfigs: () =>
    apiClient.get<{ data: IntegrationConfigDto[] }>(API_ENDPOINTS.COMMUNICATION.INTEGRATION_CONFIGS.LIST).then(r => r.data),

  getIntegrationConfig: (id: string) =>
    apiClient.get<{ data: IntegrationConfigDto }>(API_ENDPOINTS.COMMUNICATION.INTEGRATION_CONFIGS.DETAIL(id)).then(r => r.data),

  createIntegrationConfig: (data: CreateIntegrationConfigData) =>
    apiClient.post<{ data: IntegrationConfigDto }>(API_ENDPOINTS.COMMUNICATION.INTEGRATION_CONFIGS.LIST, data).then(r => r.data),

  updateIntegrationConfig: (data: UpdateIntegrationConfigData) =>
    apiClient.put<{ data: IntegrationConfigDto }>(API_ENDPOINTS.COMMUNICATION.INTEGRATION_CONFIGS.DETAIL(data.id), data).then(r => r.data),

  deleteIntegrationConfig: (id: string) =>
    apiClient.delete(API_ENDPOINTS.COMMUNICATION.INTEGRATION_CONFIGS.DETAIL(id)).then(r => r.data),

  testIntegrationConfig: (id: string) =>
    apiClient.post<{ data: TestIntegrationConfigResponse }>(API_ENDPOINTS.COMMUNICATION.INTEGRATION_CONFIGS.TEST(id)).then(r => r.data),

  // Delivery Logs
  getDeliveryLogs: (params?: Record<string, unknown>) =>
    apiClient
      .get<PaginatedResponse<DeliveryLogDto>>(API_ENDPOINTS.COMMUNICATION.DELIVERY_LOGS.LIST, { params })
      .then(r => r.data),

  getDeliveryLog: (id: string) =>
    apiClient.get<{ data: DeliveryLogDetailDto }>(API_ENDPOINTS.COMMUNICATION.DELIVERY_LOGS.DETAIL(id)).then(r => r.data),

  resendDelivery: (id: string) =>
    apiClient.post<{ data: DeliveryLogDto }>(API_ENDPOINTS.COMMUNICATION.DELIVERY_LOGS.RESEND(id)).then(r => r.data),

  getDeliveryStatusCounts: (windowDays = 7) =>
    apiClient
      .get<{ data: DeliveryStatusCountsDto }>(API_ENDPOINTS.COMMUNICATION.DELIVERY_LOGS.STATUS_COUNTS, {
        params: { windowDays },
      })
      .then(r => r.data),

  // Dashboard
  getCommunicationDashboard: () =>
    apiClient.get<{ data: CommunicationDashboardDto }>(API_ENDPOINTS.COMMUNICATION.DASHBOARD).then(r => r.data),
};
