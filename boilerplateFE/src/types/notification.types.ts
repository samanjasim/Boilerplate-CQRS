export interface Notification {
  id: string;
  type: string;
  title: string;
  message: string;
  data: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationPreference {
  notificationType: string;
  emailEnabled: boolean;
  inAppEnabled: boolean;
}
