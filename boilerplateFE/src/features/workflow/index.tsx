import { lazy } from 'react';
import type { WebModule } from '@/lib/modules';

const WorkflowStatusPanel = lazy(() =>
  import('./components/WorkflowStatusPanel').then((m) => ({ default: m.WorkflowStatusPanel })),
);

const WorkflowDashboardWidget = lazy(() =>
  import('./components/WorkflowDashboardWidget').then((m) => ({ default: m.WorkflowDashboardWidget })),
);

export const workflowModule: WebModule = {
  id: 'workflow',
  register(ctx): void {
    ctx.registerSlot('entity-detail-workflow', {
      id: 'workflow.entity-status',
      module: 'workflow',
      order: 5,
      permission: 'Workflows.View',
      component: WorkflowStatusPanel,
    });
    ctx.registerSlot('dashboard-cards', {
      id: 'workflow.pending-tasks',
      module: 'workflow',
      order: 15,
      permission: 'Workflows.View',
      component: WorkflowDashboardWidget,
    });
  },
};
