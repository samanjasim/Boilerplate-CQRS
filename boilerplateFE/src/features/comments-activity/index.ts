import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

const EntityTimeline = lazy(() =>
  import('./components/EntityTimeline').then((m) => ({ default: m.EntityTimeline })),
);

export const commentsActivityModule = {
  name: 'commentsActivity',
  register(): void {
    registerSlot('entity-detail-timeline', {
      id: 'commentsActivity.entity-timeline',
      module: 'commentsActivity',
      order: 10,
      permission: 'Comments.View',
      component: EntityTimeline,
    });
  },
};
