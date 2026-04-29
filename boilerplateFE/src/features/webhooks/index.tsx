import { lazy } from 'react';
import { Webhook } from 'lucide-react';
import { PermissionGuard } from '@/components/guards';
import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import type { WebModule } from '@/lib/modules';

/**
 * Webhooks module entry point.
 *
 * No slot contributions yet. This file establishes the registration pattern
 * for Webhooks-owned route contributions and future extension points.
 */
const WebhooksPage = lazy(() => import('./pages/WebhooksPage'));
const WebhookAdminPage = lazy(() => import('./pages/WebhookAdminPage'));
const WebhookAdminDetailPage = lazy(() => import('./pages/WebhookAdminDetailPage'));

export const webhooksModule: WebModule = {
  id: 'webhooks',
  register(ctx): void {
    ctx.registerNavGroup({
      id: 'webhooks',
      order: 60,
      build(nav) {
        if (!nav.tenantScoped || !nav.isFeatureEnabled('webhooks.enabled')) return null;

        const items = nav.hasPermission(PERMISSIONS.Webhooks.View)
          ? [{ label: nav.t('nav.webhooks'), icon: Webhook, path: ROUTES.WEBHOOKS }]
          : [];

        return { label: nav.t('nav.groups.webhooks'), items };
      },
    });

    ctx.registerNavItem('platform', {
      id: 'webhooks.admin',
      order: 90,
      build(nav) {
        if (!nav.hasPermission(PERMISSIONS.Webhooks.ViewPlatform)) return null;
        return {
          label: nav.t('nav.webhooksAdmin'),
          icon: Webhook,
          path: ROUTES.WEBHOOKS_ADMIN.LIST,
          end: true,
        };
      },
    });

    ctx.registerRoute({
      id: 'webhooks.tenant',
      region: 'protected',
      order: 30,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Webhooks.View} />,
        children: [{ path: ROUTES.WEBHOOKS, element: <WebhooksPage /> }],
      },
    });

    ctx.registerRoute({
      id: 'webhooks.admin',
      region: 'protected',
      order: 31,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Webhooks.ViewPlatform} />,
        children: [
          { path: ROUTES.WEBHOOKS_ADMIN.LIST, element: <WebhookAdminPage /> },
          {
            path: ROUTES.WEBHOOKS_ADMIN.DETAIL,
            element: <WebhookAdminDetailPage />,
          },
        ],
      },
    });
  },
};
