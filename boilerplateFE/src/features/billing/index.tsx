import { lazy } from 'react';
import { CreditCard, ListChecks, ReceiptText } from 'lucide-react';
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

    ctx.registerNavGroup({
      id: 'billing',
      order: 50,
      build(nav) {
        const items = [];
        if (nav.hasPermission(PERMISSIONS.Billing.View) && nav.tenantScoped) {
          items.push({ label: nav.t('nav.billing'), icon: CreditCard, path: ROUTES.BILLING, end: true });
        }
        if (nav.hasPermission(PERMISSIONS.Billing.ViewPlans)) {
          items.push({ label: nav.t('nav.billingPlans'), icon: ReceiptText, path: ROUTES.BILLING_PLANS });
        }
        if (nav.hasPermission(PERMISSIONS.Billing.ManageTenantSubscriptions)) {
          items.push({ label: nav.t('nav.subscriptions'), icon: ListChecks, path: ROUTES.SUBSCRIPTIONS.LIST, end: true });
        }

        return { label: nav.t('nav.groups.billing'), items };
      },
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
