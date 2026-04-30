export type NotificationChannel = 'Email' | 'Sms' | 'Push' | 'WhatsApp' | 'InApp';
export type IntegrationType = 'Slack' | 'Telegram' | 'Discord' | 'MicrosoftTeams';
export type ChannelProvider = 'Smtp' | 'SendGrid' | 'Ses' | 'Twilio' | 'Fcm' | 'Apns' | 'TwilioWhatsApp' | 'MetaWhatsApp' | 'Ably';
export type ChannelConfigStatus = 'Active' | 'Inactive' | 'Error';
export type IntegrationConfigStatus = 'Active' | 'Inactive' | 'Error';
export type DeliveryStatus = 'Pending' | 'Queued' | 'Sending' | 'Delivered' | 'Failed' | 'Bounced';
export type TriggerRuleStatus = 'Active' | 'Inactive';

export interface ChannelConfigDto {
  id: string;
  channel: NotificationChannel;
  provider: ChannelProvider;
  displayName: string;
  status: ChannelConfigStatus;
  isDefault: boolean;
  lastTestedAt: string | null;
  lastTestResult: string | null;
  createdAt: string;
  modifiedAt: string | null;
}

export interface ChannelConfigDetailDto {
  id: string;
  channel: NotificationChannel;
  provider: ChannelProvider;
  displayName: string;
  maskedCredentials: Record<string, string>;
  status: ChannelConfigStatus;
  isDefault: boolean;
  lastTestedAt: string | null;
  lastTestResult: string | null;
  createdAt: string;
  modifiedAt: string | null;
}

export interface AvailableProviderDto {
  channel: NotificationChannel;
  provider: ChannelProvider;
  displayName: string;
  requiredCredentialFields: string[];
}

export interface CreateChannelConfigData {
  channel: NotificationChannel;
  provider: ChannelProvider;
  displayName: string;
  credentials: Record<string, string>;
  isDefault: boolean;
}

export interface UpdateChannelConfigData {
  id: string;
  displayName: string;
  credentials: Record<string, string>;
}

export interface TestChannelConfigResponse {
  success: boolean;
  message: string | null;
}

export interface MessageTemplateDto {
  id: string;
  name: string;
  moduleSource: string;
  category: string;
  description: string | null;
  subjectTemplate: string | null;
  bodyTemplate: string;
  defaultChannel: NotificationChannel;
  availableChannels: string[];
  isSystem: boolean;
  hasOverride: boolean;
  createdAt: string;
  modifiedAt: string | null;
}

export interface MessageTemplateDetailDto {
  id: string;
  name: string;
  moduleSource: string;
  category: string;
  description: string | null;
  subjectTemplate: string | null;
  bodyTemplate: string;
  defaultChannel: NotificationChannel;
  availableChannels: string[];
  variableSchema: Record<string, string> | null;
  sampleVariables: Record<string, unknown> | null;
  isSystem: boolean;
  override: MessageTemplateOverrideDto | null;
  createdAt: string;
  modifiedAt: string | null;
}

export interface MessageTemplateOverrideDto {
  id: string;
  subjectTemplate: string | null;
  bodyTemplate: string;
  isActive: boolean;
  createdAt: string;
  modifiedAt: string | null;
}

export interface TemplatePreviewDto {
  renderedSubject: string;
  renderedBody: string;
}

export interface CreateTemplateOverrideData {
  subjectTemplate: string | null;
  bodyTemplate: string;
}

export interface UpdateTemplateOverrideData {
  subjectTemplate: string | null;
  bodyTemplate: string;
}

export interface TriggerRuleDto {
  id: string;
  name: string;
  eventName: string;
  messageTemplateId: string;
  messageTemplateName: string | null;
  recipientMode: string;
  channelSequence: string[];
  delaySeconds: number;
  status: TriggerRuleStatus;
  integrationTargetCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface EventRegistrationDto {
  id: string;
  eventName: string;
  moduleSource: string;
  displayName: string;
  description: string | null;
}

export interface CreateTriggerRuleData {
  name: string;
  eventName: string;
  messageTemplateId: string;
  recipientMode: string;
  channelSequence: string[];
  delaySeconds: number;
  conditionJson?: string | null;
}

export interface UpdateTriggerRuleData extends CreateTriggerRuleData {
  id: string;
}

export interface NotificationPreferenceDto {
  userId: string;
  category: string;
  emailEnabled: boolean;
  smsEnabled: boolean;
  pushEnabled: boolean;
  whatsAppEnabled: boolean;
  inAppEnabled: boolean;
}

export interface RequiredNotificationDto {
  id: string;
  category: string;
  channel: NotificationChannel;
  createdAt: string;
}

export interface NotificationPreferenceItem {
  category: string;
  emailEnabled: boolean;
  smsEnabled: boolean;
  pushEnabled: boolean;
  whatsAppEnabled: boolean;
  inAppEnabled: boolean;
}

// Integration Configs
export interface IntegrationConfigDto {
  id: string;
  integrationType: IntegrationType;
  displayName: string;
  maskedCredentials?: Record<string, string>;
  channelMappings: Record<string, string> | null;
  status: IntegrationConfigStatus;
  lastTestedAt: string | null;
  lastTestResult: string | null;
  createdAt: string;
  modifiedAt: string | null;
}

export interface CreateIntegrationConfigData {
  integrationType: IntegrationType;
  displayName: string;
  credentials: Record<string, string>;
  channelMappingsJson?: string | null;
}

export interface UpdateIntegrationConfigData {
  id: string;
  displayName: string;
  credentials: Record<string, string>;
  channelMappingsJson?: string | null;
}

export interface TestIntegrationConfigResponse {
  success: boolean;
  message: string | null;
}

// Delivery Log
export interface DeliveryLogDto {
  id: string;
  recipientUserId: string | null;
  recipientAddress: string | null;
  templateName: string;
  channel: NotificationChannel | null;
  integrationType: IntegrationType | null;
  provider: ChannelProvider | null;
  subject: string | null;
  bodyPreview: string | null;
  status: DeliveryStatus;
  providerMessageId: string | null;
  errorMessage: string | null;
  totalDurationMs: number | null;
  attemptCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface DeliveryLogDetailDto extends DeliveryLogDto {
  messageTemplateId: string | null;
  triggerRuleId: string | null;
  attempts: DeliveryAttemptDto[];
}

export interface DeliveryAttemptDto {
  id: string;
  attemptNumber: number;
  channel: NotificationChannel | null;
  integrationType: IntegrationType | null;
  provider: ChannelProvider | null;
  status: DeliveryStatus;
  providerResponse: string | null;
  errorMessage: string | null;
  durationMs: number | null;
  attemptedAt: string;
}

export interface CommunicationDashboardDto {
  messagesSentToday: number;
  messagesSentThisWeek: number;
  messagesSentThisMonth: number;
  deliverySuccessRate: number;
  channelBreakdown: Record<string, number>;
  failedDeliveries: number;
  quotaUsed: number | null;
  quotaLimit: number | null;
}

export interface DeliveryStatusCountsDto {
  delivered: number;
  failed: number;
  pending: number;
  bounced: number;
  windowDays: number;
}
