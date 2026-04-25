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

      // Maps notification type → query keys to invalidate.
      // Add a new type → resource invalidation here when BE adds new push events.
      const TYPE_INVALIDATIONS: Record<string, ReadonlyArray<readonly unknown[]>> = {
        ReportReady: [queryKeys.reports.all],
        ReportFailed: [queryKeys.reports.all],
        ReportCompleted: [queryKeys.reports.all],
        import_completed: [queryKeys.importExport.imports.all],
        import_partial: [queryKeys.importExport.imports.all],
        import_failed: [queryKeys.importExport.imports.all],
        WorkflowTaskAssigned: [queryKeys.workflow.tasks.all],
      };

      channel.subscribe('notification', (message) => {
        try {
          const data = typeof message.data === 'string' ? JSON.parse(message.data) : message.data;

          // Always refresh notification list + unread badge
          queryClient.invalidateQueries({ queryKey: queryKeys.notifications.all });

          const notificationType = data?.type ?? data?.notificationType ?? '';
          for (const key of TYPE_INVALIDATIONS[notificationType] ?? []) {
            queryClient.invalidateQueries({ queryKey: key });
          }

          if (data?.title) {
            toast.info(data.title, {
              description: data.message,
            });
          }
        } catch {
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
