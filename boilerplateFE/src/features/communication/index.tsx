import { lazy } from 'react';
import { FileText, Link2, MessageSquare, ScrollText, Zap } from 'lucide-react';
import { PermissionGuard } from '@/components/guards';
import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import type { WebModule } from '@/lib/modules';

const CommunicationDashboardWidget = lazy(() =>
  import('./components/CommunicationDashboardWidget').then((m) => ({
    default: m.CommunicationDashboardWidget,
  })),
);

const ChannelsPage = lazy(() => import('./pages/ChannelsPage'));
const TemplatesPage = lazy(() => import('./pages/TemplatesPage'));
const TriggerRulesPage = lazy(() => import('./pages/TriggerRulesPage'));
const IntegrationsPage = lazy(() => import('./pages/IntegrationsPage'));
const DeliveryLogPage = lazy(() => import('./pages/DeliveryLogPage'));

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

    ctx.registerNavGroup({
      id: 'communication',
      order: 30,
      build(nav) {
        if (!nav.tenantScoped) return null;

        const items = [];
        if (nav.hasPermission(PERMISSIONS.Communication.View)) {
          items.push({ label: nav.t('nav.channels'), icon: MessageSquare, path: ROUTES.COMMUNICATION.CHANNELS });
          items.push({ label: nav.t('nav.templates'), icon: FileText, path: ROUTES.COMMUNICATION.TEMPLATES });
          items.push({ label: nav.t('nav.triggerRules'), icon: Zap, path: ROUTES.COMMUNICATION.TRIGGER_RULES });
          items.push({ label: nav.t('nav.integrations'), icon: Link2, path: ROUTES.COMMUNICATION.INTEGRATIONS });
        }
        if (nav.hasPermission(PERMISSIONS.Communication.ViewDeliveryLog)) {
          items.push({ label: nav.t('nav.deliveryLog'), icon: ScrollText, path: ROUTES.COMMUNICATION.DELIVERY_LOG });
        }

        return { label: nav.t('nav.groups.communication'), items };
      },
    });

    ctx.registerRoute({
      id: 'communication.main',
      region: 'protected',
      order: 70,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Communication.View} />,
        children: [
          { path: ROUTES.COMMUNICATION.CHANNELS, element: <ChannelsPage /> },
          { path: ROUTES.COMMUNICATION.TEMPLATES, element: <TemplatesPage /> },
          {
            path: ROUTES.COMMUNICATION.TRIGGER_RULES,
            element: <TriggerRulesPage />,
          },
          {
            path: ROUTES.COMMUNICATION.INTEGRATIONS,
            element: <IntegrationsPage />,
          },
        ],
      },
    });

    ctx.registerRoute({
      id: 'communication.deliveryLog',
      region: 'protected',
      order: 71,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Communication.ViewDeliveryLog} />,
        children: [
          {
            path: ROUTES.COMMUNICATION.DELIVERY_LOG,
            element: <DeliveryLogPage />,
          },
        ],
      },
    });
  },
};
