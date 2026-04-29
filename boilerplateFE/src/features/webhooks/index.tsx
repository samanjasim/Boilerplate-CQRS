import { lazy } from 'react';
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
