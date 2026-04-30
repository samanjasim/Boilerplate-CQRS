import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { queryKeys } from '@/lib/query/keys';
import { communicationApi } from './communication.api';
import type { CreateChannelConfigData, UpdateChannelConfigData, CreateTemplateOverrideData, UpdateTemplateOverrideData, CreateTriggerRuleData, UpdateTriggerRuleData, NotificationPreferenceItem, NotificationChannel, CreateIntegrationConfigData, UpdateIntegrationConfigData } from '@/types/communication.types';

// ── Channel Configs ──

export function useChannelConfigs() {
  return useQuery({
    queryKey: queryKeys.communication.channelConfigs.list(),
    queryFn: () => communicationApi.getChannelConfigs(),
  });
}

export function useChannelConfig(id: string) {
  return useQuery({
    queryKey: queryKeys.communication.channelConfigs.detail(id),
    queryFn: () => communicationApi.getChannelConfig(id),
    enabled: !!id,
  });
}

export function useAvailableProviders() {
  return useQuery({
    queryKey: queryKeys.communication.providers(),
    queryFn: () => communicationApi.getAvailableProviders(),
    staleTime: Infinity,
  });
}

export function useCreateChannelConfig() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (data: CreateChannelConfigData) => communicationApi.createChannelConfig(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.channelConfigs.all });
      toast.success(t('communication.channels.created'));
    },
  });
}

export function useUpdateChannelConfig() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (data: UpdateChannelConfigData) => communicationApi.updateChannelConfig(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.channelConfigs.all });
      toast.success(t('communication.channels.updated'));
    },
  });
}

export function useDeleteChannelConfig() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.deleteChannelConfig(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.channelConfigs.all });
      toast.success(t('communication.channels.deleted'));
    },
  });
}

export function useTestChannelConfig() {
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.testChannelConfig(id),
    onSuccess: (data) => {
      if (data.data.success) {
        toast.success(t('communication.channels.testSuccess'));
      } else {
        toast.error(data.data.message || t('communication.channels.testFailed'));
      }
    },
  });
}

export function useSetDefaultChannelConfig() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.setDefaultChannelConfig(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.channelConfigs.all });
      toast.success(t('communication.channels.setDefault'));
    },
  });
}

// ── Templates ──

export function useMessageTemplates(category?: string) {
  return useQuery({
    queryKey: queryKeys.communication.templates.list(category),
    queryFn: () => communicationApi.getMessageTemplates(category),
  });
}

export function useMessageTemplate(id: string) {
  return useQuery({
    queryKey: queryKeys.communication.templates.detail(id),
    queryFn: () => communicationApi.getMessageTemplate(id),
    enabled: !!id,
  });
}

export function useTemplateCategories() {
  return useQuery({
    queryKey: queryKeys.communication.templates.categories(),
    queryFn: () => communicationApi.getTemplateCategories(),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateTemplateOverride() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: CreateTemplateOverrideData }) =>
      communicationApi.createTemplateOverride(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.templates.all });
      toast.success(t('communication.templates.overrideCreated'));
    },
  });
}

export function useUpdateTemplateOverride() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateTemplateOverrideData }) =>
      communicationApi.updateTemplateOverride(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.templates.all });
      toast.success(t('communication.templates.overrideUpdated'));
    },
  });
}

export function useDeleteTemplateOverride() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.deleteTemplateOverride(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.templates.all });
      toast.success(t('communication.templates.overrideDeleted'));
    },
  });
}

export function usePreviewTemplate() {
  return useMutation({
    mutationFn: ({ id, variables }: { id: string; variables?: Record<string, unknown> }) =>
      communicationApi.previewTemplate(id, variables),
  });
}

// ── Trigger Rules ──

export function useTriggerRules() {
  return useQuery({
    queryKey: queryKeys.communication.triggerRules.list(),
    queryFn: () => communicationApi.getTriggerRules(),
  });
}

export function useTriggerRule(id: string) {
  return useQuery({
    queryKey: queryKeys.communication.triggerRules.detail(id),
    queryFn: () => communicationApi.getTriggerRule(id),
    enabled: !!id,
  });
}

export function useRegisteredEvents() {
  return useQuery({
    queryKey: queryKeys.communication.events.list(),
    queryFn: () => communicationApi.getRegisteredEvents(),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateTriggerRule() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (data: CreateTriggerRuleData) => communicationApi.createTriggerRule(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.triggerRules.all });
      toast.success(t('communication.triggerRules.created'));
    },
  });
}

export function useUpdateTriggerRule() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (data: UpdateTriggerRuleData) => communicationApi.updateTriggerRule(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.triggerRules.all });
      toast.success(t('communication.triggerRules.updated'));
    },
  });
}

export function useDeleteTriggerRule() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.deleteTriggerRule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.triggerRules.all });
      toast.success(t('communication.triggerRules.deleted'));
    },
  });
}

export function useToggleTriggerRule() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.toggleTriggerRule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.triggerRules.all });
      toast.success(t('communication.triggerRules.toggled'));
    },
  });
}

// ── Notification Preferences ──

export function useNotificationPreferences() {
  return useQuery({
    queryKey: queryKeys.communication.preferences.list(),
    queryFn: () => communicationApi.getNotificationPreferences(),
  });
}

export function useUpdateNotificationPreferences() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (preferences: NotificationPreferenceItem[]) =>
      communicationApi.updateNotificationPreferences(preferences),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.preferences.all });
      toast.success(t('communication.preferences.saved'));
    },
  });
}

// ── Required Notifications ──

export function useRequiredNotifications() {
  return useQuery({
    queryKey: queryKeys.communication.required.list(),
    queryFn: () => communicationApi.getRequiredNotifications(),
  });
}

export function useSetRequiredNotification() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (data: { category: string; channel: NotificationChannel }) =>
      communicationApi.setRequiredNotification(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.required.all });
      toast.success(t('communication.requiredNotifications.added'));
    },
  });
}

export function useRemoveRequiredNotification() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.removeRequiredNotification(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.required.all });
      toast.success(t('communication.requiredNotifications.removed'));
    },
  });
}

// ── Integration Configs ──

export function useIntegrationConfigs() {
  return useQuery({
    queryKey: queryKeys.communication.integrationConfigs.list(),
    queryFn: () => communicationApi.getIntegrationConfigs(),
  });
}

export function useIntegrationConfig(id: string) {
  return useQuery({
    queryKey: queryKeys.communication.integrationConfigs.detail(id),
    queryFn: () => communicationApi.getIntegrationConfig(id),
    enabled: !!id,
  });
}

export function useCreateIntegrationConfig() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (data: CreateIntegrationConfigData) => communicationApi.createIntegrationConfig(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.integrationConfigs.all });
      toast.success(t('communication.integrations.created'));
    },
  });
}

export function useUpdateIntegrationConfig() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (data: UpdateIntegrationConfigData) => communicationApi.updateIntegrationConfig(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.integrationConfigs.all });
      toast.success(t('communication.integrations.updated'));
    },
  });
}

export function useDeleteIntegrationConfig() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.deleteIntegrationConfig(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.integrationConfigs.all });
      toast.success(t('communication.integrations.deleted'));
    },
  });
}

export function useTestIntegrationConfig() {
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.testIntegrationConfig(id),
    onSuccess: (data) => {
      if (data.data.success) {
        toast.success(t('communication.integrations.testSuccess'));
      } else {
        toast.error(data.data.message || t('communication.integrations.testFailed'));
      }
    },
  });
}

// ── Delivery Logs ──

export function useDeliveryLogs(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.communication.deliveryLogs.list(params),
    queryFn: () => communicationApi.getDeliveryLogs(params),
  });
}

export function useDeliveryLog(id: string) {
  return useQuery({
    queryKey: queryKeys.communication.deliveryLogs.detail(id),
    queryFn: () => communicationApi.getDeliveryLog(id),
    enabled: !!id,
  });
}

export function useResendDelivery() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  return useMutation({
    mutationFn: (id: string) => communicationApi.resendDelivery(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.communication.deliveryLogs.all });
      toast.success(t('communication.deliveryLog.resendSuccess'));
    },
  });
}

export function useDeliveryStatusCounts(windowDays = 7) {
  return useQuery({
    queryKey: queryKeys.communication.deliveryLogs.statusCounts(windowDays),
    queryFn: () => communicationApi.getDeliveryStatusCounts(windowDays),
    staleTime: 30_000,
  });
}

// ── Dashboard ──

export function useCommunicationDashboard() {
  return useQuery({
    queryKey: queryKeys.communication.dashboard(),
    queryFn: () => communicationApi.getCommunicationDashboard(),
    staleTime: 60 * 1000, // 1 minute
  });
}
