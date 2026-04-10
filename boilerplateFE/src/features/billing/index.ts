import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

/**
 * Billing module entry point.
 *
 * Registers slot contributions at app bootstrap. The lazy() wrapper ensures
 * the component code is only fetched when the slot is actually rendered, so
 * the module is fully tree-shakeable when removed from `modules.config.ts`.
 */
const TenantSubscriptionTab = lazy(() =>
  import('./components/TenantSubscriptionTab').then((m) => ({ default: m.TenantSubscriptionTab })),
);

export const billingModule = {
  name: 'billing',
  register(): void {
    registerSlot('tenant-detail-tabs', {
      id: 'billing.tenant-subscription',
      module: 'billing',
      order: 30,
      label: () => 'Subscription',
      permission: 'Billing.View',
      component: TenantSubscriptionTab,
    });
  },
};
