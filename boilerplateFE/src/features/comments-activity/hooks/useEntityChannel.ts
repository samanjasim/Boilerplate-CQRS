import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { useAuthStore, selectUser } from '@/stores';
import * as Ably from 'ably';

let missingKeyWarned = false;

export function useEntityChannel(
  entityType: string,
  entityId: string,
  entityTenantId?: string,
): { connected: boolean } {
  const [connected, setConnected] = useState(false);
  const queryClient = useQueryClient();
  const user = useAuthStore(selectUser);

  // Use entity's tenantId (for platform admins viewing tenant entities) or user's tenantId
  const tenantId = entityTenantId ?? user?.tenantId;

  useEffect(() => {
    const ABLY_API_KEY = import.meta.env.VITE_ABLY_API_KEY;
    if (!ABLY_API_KEY) {
      if (!missingKeyWarned) {
        missingKeyWarned = true;
        // eslint-disable-next-line no-console
        console.warn(
          '[comments-activity] VITE_ABLY_API_KEY missing — realtime updates disabled.',
        );
      }
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setConnected(false);
      return;
    }
    if (!tenantId || !entityType || !entityId) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setConnected(false);
      return;
    }

    // Coalesce bursts of events: rapid-fire messages (e.g. a conversation thread
    // catching up) would otherwise trigger one invalidation per message and
    // refetch the paged timeline repeatedly.
    let pending: Set<'timeline' | 'comments' | 'activity'> = new Set();
    let flushHandle: ReturnType<typeof setTimeout> | null = null;
    const DEBOUNCE_MS = 150;

    const scheduleFlush = (keys: ('timeline' | 'comments' | 'activity')[]) => {
      keys.forEach((k) => pending.add(k));
      if (flushHandle) return;
      flushHandle = setTimeout(() => {
        const toInvalidate = pending;
        pending = new Set();
        flushHandle = null;
        if (toInvalidate.has('timeline')) {
          queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
        }
        if (toInvalidate.has('comments')) {
          queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
        }
        if (toInvalidate.has('activity')) {
          queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.activity.all });
        }
      }, DEBOUNCE_MS);
    };

    try {
      const client = new Ably.Realtime({ key: ABLY_API_KEY });
      const channelName = `entity-${tenantId}-${entityType}-${entityId}`;
      const channel = client.channels.get(channelName);

      client.connection.on('connected', () => setConnected(true));
      client.connection.on('disconnected', () => setConnected(false));
      client.connection.on('failed', () => setConnected(false));

      const onCommentEvent = () => scheduleFlush(['timeline', 'comments']);
      const onActivityEvent = () => scheduleFlush(['timeline', 'activity']);

      channel.subscribe('comment:created', onCommentEvent);
      channel.subscribe('comment:updated', onCommentEvent);
      channel.subscribe('comment:deleted', onCommentEvent);
      channel.subscribe('activity:created', onActivityEvent);
      channel.subscribe('reaction:changed', onCommentEvent);

      return () => {
        if (flushHandle) clearTimeout(flushHandle);
        channel.unsubscribe();
        client.close();
        setConnected(false);
      };
    } catch {
      setConnected(false);
    }
  }, [entityType, entityId, tenantId, queryClient]);

  return { connected };
}
