import { lazy } from 'react';
import type { WebModule } from '@/lib/modules';

const CommunicationDashboardWidget = lazy(() =>
  import('./components/CommunicationDashboardWidget').then((m) => ({ default: m.CommunicationDashboardWidget })),
);

export const communicationModule: WebModule = {
  id: 'communication',
  register(ctx): void {
    ctx.registerSlot('dashboard-cards', {
      id: 'communication.dashboard',
      module: 'communication',
      order: 40,
      permission: 'Communication.View',
      component: CommunicationDashboardWidget,
    });
  },
};
