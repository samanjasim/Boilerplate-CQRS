import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

const EntityTimelineSlot = lazy(() =>
  import('./components/EntityTimelineSlot').then((m) => ({ default: m.EntityTimelineSlot })),
);

export const commentsActivityModule = {
  name: 'commentsActivity',
  register(): void {
    registerSlot('entity-detail-timeline', {
      id: 'commentsActivity.entity-timeline',
      module: 'commentsActivity',
      order: 10,
      permission: 'Comments.View',
      component: EntityTimelineSlot,
    });
  },
};
