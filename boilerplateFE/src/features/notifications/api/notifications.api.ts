import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  Notification,
  NotificationPreference,
  ApiResponse,
  PaginatedResponse,
  PaginationParams,
} from '@/types';

export const notificationsApi = {
  getNotifications: async (
    params?: PaginationParams & { isRead?: boolean }
  ): Promise<PaginatedResponse<Notification>> => {
    const response = await apiClient.get<PaginatedResponse<Notification>>(
      API_ENDPOINTS.NOTIFICATIONS.LIST,
      { params }
    );
    return response.data;
  },

  getUnreadCount: async (): Promise<number> => {
    const response = await apiClient.get<ApiResponse<number>>(
      API_ENDPOINTS.NOTIFICATIONS.UNREAD_COUNT
    );
    return response.data.data;
  },

  markRead: async (id: string): Promise<void> => {
    await apiClient.post(API_ENDPOINTS.NOTIFICATIONS.MARK_READ(id));
  },

  markAllRead: async (): Promise<void> => {
    await apiClient.post(API_ENDPOINTS.NOTIFICATIONS.MARK_ALL_READ);
  },

  getPreferences: async (): Promise<NotificationPreference[]> => {
    const response = await apiClient.get<ApiResponse<NotificationPreference[]>>(
      API_ENDPOINTS.NOTIFICATIONS.PREFERENCES
    );
    return response.data.data;
  },

  updatePreferences: async (preferences: NotificationPreference[]): Promise<void> => {
    await apiClient.put(API_ENDPOINTS.NOTIFICATIONS.PREFERENCES, { preferences });
  },
};
