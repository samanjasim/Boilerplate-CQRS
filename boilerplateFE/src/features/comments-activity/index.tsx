import { lazy } from 'react';
import type { WebModule } from '@/lib/modules';

const EntityTimelineSlot = lazy(() =>
  import('./components/EntityTimelineSlot').then((m) => ({ default: m.EntityTimelineSlot })),
);

export const commentsActivityModule: WebModule = {
  id: 'commentsActivity',
  register(ctx): void {
    ctx.registerSlot('entity-detail-timeline', {
      id: 'commentsActivity.entity-timeline',
      module: 'commentsActivity',
      order: 10,
      permission: 'Comments.View',
      component: EntityTimelineSlot,
    });
  },
};
