import { lazy } from 'react';
import { PermissionGuard } from '@/components/guards';
import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import type { WebModule } from '@/lib/modules';

/**
 * Billing module entry point.
 *
 * Registers slot contributions at app bootstrap. The lazy() wrapper ensures
 * the component code is only fetched when the slot is actually rendered, so
 * the module is fully tree-shakeable when removed from `modules.config.ts`.
 */
const TenantSubscriptionTab = lazy(() =>
  import('./components/TenantSubscriptionTab').then((m) => ({
    default: m.TenantSubscriptionTab,
  })),
);

const PricingPage = lazy(() => import('./pages/PricingPage'));
const BillingPage = lazy(() => import('./pages/BillingPage'));
const BillingPlansPage = lazy(() => import('./pages/BillingPlansPage'));
const SubscriptionsPage = lazy(() => import('./pages/SubscriptionsPage'));
const SubscriptionDetailPage = lazy(() => import('./pages/SubscriptionDetailPage'));

export const billingModule: WebModule = {
  id: 'billing',
  register(ctx): void {
    ctx.registerSlot('tenant-detail-tabs', {
      id: 'billing.tenant-subscription',
      module: 'billing',
      order: 30,
      label: () => 'Subscription',
      permission: 'Billing.View',
      component: TenantSubscriptionTab,
    });

    ctx.registerRoute({
      id: 'billing.pricing',
      region: 'public',
      order: 20,
      route: { path: ROUTES.PRICING, element: <PricingPage /> },
    });

    ctx.registerRoute({
      id: 'billing.tenant',
      region: 'protected',
      order: 80,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Billing.View} />,
        children: [{ path: ROUTES.BILLING, element: <BillingPage /> }],
      },
    });

    ctx.registerRoute({
      id: 'billing.plans',
      region: 'protected',
      order: 81,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Billing.ViewPlans} />,
        children: [{ path: ROUTES.BILLING_PLANS, element: <BillingPlansPage /> }],
      },
    });

    ctx.registerRoute({
      id: 'billing.subscriptions',
      region: 'protected',
      order: 82,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Billing.ManageTenantSubscriptions} />,
        children: [
          { path: ROUTES.SUBSCRIPTIONS.LIST, element: <SubscriptionsPage /> },
          {
            path: ROUTES.SUBSCRIPTIONS.DETAIL,
            element: <SubscriptionDetailPage />,
          },
        ],
      },
    });
  },
};
