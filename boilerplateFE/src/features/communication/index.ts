import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

const CommunicationDashboardWidget = lazy(() =>
  import('./components/CommunicationDashboardWidget').then((m) => ({ default: m.CommunicationDashboardWidget })),
);

export const communicationModule = {
  name: 'communication',
  register(): void {
    registerSlot('dashboard-cards', {
      id: 'communication.dashboard',
      module: 'communication',
      order: 40,
      permission: 'Communication.View',
      component: CommunicationDashboardWidget,
    });
  },
};
