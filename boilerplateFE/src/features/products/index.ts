import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

const TenantProductsTab = lazy(() =>
  import('./components/TenantProductsTab').then((m) => ({ default: m.TenantProductsTab })),
);

const ProductsDashboardCard = lazy(() =>
  import('./components/ProductsDashboardCard').then((m) => ({ default: m.ProductsDashboardCard })),
);

export const productsModule = {
  name: 'products',
  register(): void {
    registerSlot('tenant-detail-tabs', {
      id: 'products.tenant-products',
      module: 'products',
      order: 40,
      label: () => 'Products',
      permission: 'Products.View',
      component: TenantProductsTab,
    });

    registerSlot('dashboard-cards', {
      id: 'products.dashboard-card',
      module: 'products',
      order: 10,
      permission: 'Products.View',
      component: ProductsDashboardCard,
    });
  },
};
