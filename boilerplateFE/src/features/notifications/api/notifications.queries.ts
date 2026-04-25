import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { notificationsApi } from './notifications.api';
import { queryKeys } from '@/lib/query/keys';
import i18n from '@/i18n';
import type { PaginationParams } from '@/types';

export function useNotifications(params?: PaginationParams & { isRead?: boolean }, options?: { refetchInterval?: number | false }) {
  return useQuery({
    queryKey: queryKeys.notifications.list(params ?? {}),
    queryFn: () => notificationsApi.getNotifications(params),
    refetchInterval: options?.refetchInterval,
  });
}

/**
 * Polls the unread count every 30s by default. Pass `refetchInterval: false`
 * when Ably push is connected so we don't double-poll the server — the Ably
 * channel will invalidate this query on the relevant events.
 */
export function useUnreadCount(options?: { refetchInterval?: number | false }) {
  return useQuery({
    queryKey: queryKeys.notifications.unreadCount(),
    queryFn: () => notificationsApi.getUnreadCount(),
    refetchInterval: options?.refetchInterval ?? 30000,
  });
}

export function useMarkRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
    },
  });
}

export function useMarkAllRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
      toast.success(i18n.t('notifications.allMarkedRead'));
    },
  });
}

export function useNotificationPreferences() {
  return useQuery({
    queryKey: queryKeys.notifications.preferences(),
    queryFn: () => notificationsApi.getPreferences(),
  });
}

export function useUpdateNotificationPreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: notificationsApi.updatePreferences,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.notifications.preferences() });
      toast.success(i18n.t('notifications.preferencesSaved'));
    },
  });
}
