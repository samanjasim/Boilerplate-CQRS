import { lazy } from 'react';
import { PermissionGuard } from '@/components/guards';
import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import type { WebModule } from '@/lib/modules';

const TenantProductsTab = lazy(() =>
  import('./components/TenantProductsTab').then((m) => ({
    default: m.TenantProductsTab,
  })),
);

const ProductsDashboardCard = lazy(() =>
  import('./components/ProductsDashboardCard').then((m) => ({
    default: m.ProductsDashboardCard,
  })),
);

const ProductsListPage = lazy(() => import('./pages/ProductsListPage'));
const ProductCreatePage = lazy(() => import('./pages/ProductCreatePage'));
const ProductDetailPage = lazy(() => import('./pages/ProductDetailPage'));

export const productsModule: WebModule = {
  id: 'products',
  register(ctx): void {
    ctx.registerSlot('tenant-detail-tabs', {
      id: 'products.tenant-products',
      module: 'products',
      order: 40,
      label: () => 'Products',
      permission: 'Products.View',
      component: TenantProductsTab,
    });

    ctx.registerSlot('dashboard-cards', {
      id: 'products.dashboard-card',
      module: 'products',
      order: 10,
      permission: 'Products.View',
      component: ProductsDashboardCard,
    });

    ctx.registerRoute({
      id: 'products.view',
      region: 'protected',
      order: 50,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Products.View} />,
        children: [
          { path: ROUTES.PRODUCTS.LIST, element: <ProductsListPage /> },
          { path: ROUTES.PRODUCTS.DETAIL, element: <ProductDetailPage /> },
        ],
      },
    });

    ctx.registerRoute({
      id: 'products.create',
      region: 'protected',
      order: 51,
      route: {
        element: <PermissionGuard permission={PERMISSIONS.Products.Create} />,
        children: [{ path: ROUTES.PRODUCTS.CREATE, element: <ProductCreatePage /> }],
      },
    });
  },
};
