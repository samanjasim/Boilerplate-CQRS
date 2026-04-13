import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { useAuthStore, selectUser } from '@/stores';
import * as Ably from 'ably';

export function useEntityChannel(
  entityType: string,
  entityId: string,
): { connected: boolean } {
  const [connected, setConnected] = useState(false);
  const queryClient = useQueryClient();
  const user = useAuthStore(selectUser);

  useEffect(() => {
    const ABLY_API_KEY = import.meta.env.VITE_ABLY_API_KEY;
    if (!ABLY_API_KEY || !user?.tenantId || !entityType || !entityId) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setConnected(false);
      return;
    }

    try {
      const client = new Ably.Realtime({ key: ABLY_API_KEY });
      const channelName = `entity-${user.tenantId}-${entityType}-${entityId}`;
      const channel = client.channels.get(channelName);

      client.connection.on('connected', () => setConnected(true));
      client.connection.on('disconnected', () => setConnected(false));
      client.connection.on('failed', () => setConnected(false));

      const invalidateAll = () => {
        queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
        queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      };

      channel.subscribe('comment:created', invalidateAll);
      channel.subscribe('comment:updated', invalidateAll);
      channel.subscribe('comment:deleted', invalidateAll);
      channel.subscribe('activity:created', () => {
        queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
        queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.activity.all });
      });
      channel.subscribe('reaction:changed', invalidateAll);

      return () => {
        channel.unsubscribe();
        client.close();
        setConnected(false);
      };
    } catch {
      setConnected(false);
    }
  }, [entityType, entityId, user?.tenantId, queryClient]);

  return { connected };
}
