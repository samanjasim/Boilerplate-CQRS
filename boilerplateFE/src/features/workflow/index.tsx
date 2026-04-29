import { lazy } from 'react';
import { PermissionGuard } from '@/components/guards';
import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import type { WebModule } from '@/lib/modules';

const WorkflowStatusPanel = lazy(() =>
  import('./components/WorkflowStatusPanel').then((m) => ({
    default: m.WorkflowStatusPanel,
  })),
);

const WorkflowDashboardWidget = lazy(() =>
  import('./components/WorkflowDashboardWidget').then((m) => ({
    default: m.WorkflowDashboardWidget,
  })),
);

const WorkflowInboxPage = lazy(() => import('./pages/WorkflowInboxPage'));
const WorkflowInstancesPage = lazy(() => import('./pages/WorkflowInstancesPage'));
const WorkflowInstanceDetailPage = lazy(() => import('./pages/WorkflowInstanceDetailPage'));
const WorkflowDefinitionsPage = lazy(() => import('./pages/WorkflowDefinitionsPage'));
const WorkflowDefinitionDetailPage = lazy(() => import('./pages/WorkflowDefinitionDetailPage'));
const WorkflowDefinitionDesignerPage = lazy(() => import('./pages/WorkflowDefinitionDesignerPage'));

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

    ctx.registerRoute({
      id: 'workflow.instances',
      region: 'protected',
      order: 60,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Workflows.View} />,
        children: [
          { path: ROUTES.WORKFLOWS.INBOX, element: <WorkflowInboxPage /> },
          {
            path: ROUTES.WORKFLOWS.INSTANCES,
            element: <WorkflowInstancesPage />,
          },
          {
            path: ROUTES.WORKFLOWS.INSTANCE_DETAIL,
            element: <WorkflowInstanceDetailPage />,
          },
        ],
      },
    });

    ctx.registerRoute({
      id: 'workflow.definitions',
      region: 'protected',
      order: 61,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Workflows.ManageDefinitions} />,
        children: [
          {
            path: ROUTES.WORKFLOWS.DEFINITIONS,
            element: <WorkflowDefinitionsPage />,
          },
          {
            path: ROUTES.WORKFLOWS.DEFINITION_DETAIL,
            element: <WorkflowDefinitionDetailPage />,
          },
          {
            path: ROUTES.WORKFLOWS.DEFINITION_DESIGNER,
            element: <WorkflowDefinitionDesignerPage />,
          },
        ],
      },
    });
  },
};
