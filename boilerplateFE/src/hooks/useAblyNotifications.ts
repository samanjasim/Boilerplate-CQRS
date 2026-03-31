import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { queryKeys } from '@/lib/query/keys';
import { useAuthStore, selectUser } from '@/stores';
import * as Ably from 'ably';

export function useAblyNotifications(): { connected: boolean } {
  const [connected, setConnected] = useState(false);
  const queryClient = useQueryClient();
  const user = useAuthStore(selectUser);

  useEffect(() => {
    const ABLY_API_KEY = import.meta.env.VITE_ABLY_API_KEY;
    if (!ABLY_API_KEY || !user?.id) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setConnected(false);
      return;
    }

    try {
      const client = new Ably.Realtime({ key: ABLY_API_KEY });
      const channel = client.channels.get(`user-${user.id}`);

      client.connection.on('connected', () => setConnected(true));
      client.connection.on('disconnected', () => setConnected(false));
      client.connection.on('failed', () => setConnected(false));

      channel.subscribe('notification', (message) => {
        try {
          const data = typeof message.data === 'string' ? JSON.parse(message.data) : message.data;

          // Invalidate notification queries to refresh the list and unread count
          queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });

          // If this is a report-related notification, also invalidate reports
          const notificationType = data?.type ?? data?.notificationType ?? '';
          if (
            notificationType === 'ReportReady' ||
            notificationType === 'ReportFailed' ||
            notificationType === 'ReportCompleted'
          ) {
            queryClient.invalidateQueries({ queryKey: queryKeys.reports.all });
          }

          // Show a toast for the new notification
          if (data?.title) {
            toast.info(data.title, {
              description: data.message,
            });
          }
        } catch {
          // Still invalidate on parse error
          queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });
        }
      });

      return () => {
        channel.unsubscribe();
        client.close();
        setConnected(false);
      };
    } catch {
      setConnected(false);
    }
  }, [user?.id, queryClient]);

  return { connected };
}
