import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ImageIcon, Package, Plus } from 'lucide-react';
import { PageHeader, Pagination, ListPageState, ListToolbar } from '@/components/common';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ROUTES } from '@/config';
import { usePermissions, useListPage } from '@/hooks';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import { useAuthStore, selectUser } from '@/stores';
import { useTenants } from '@/features/tenants/api';
import { useProducts } from '../api';
import { useFileUrl } from '@/features/files/api';
import type { Product } from '@/types';

interface ProductFilters {
  searchTerm?: string;
  status?: string;
  tenantId?: string;
}

export default function ProductsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);
  const isPlatformAdmin = !user?.tenantId;

  const list = useListPage<ProductFilters, Product>({ queryHook: useProducts });

  const { data: tenantsData } = useTenants(isPlatformAdmin ? { pageSize: 100 } : undefined);
  const tenants = tenantsData?.data ?? [];

  const canCreate = hasPermission(PERMISSIONS.Products.Create);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('products.title', 'Products')}
        subtitle={t('products.subtitle', 'Manage your product catalog')}
        actions={
          canCreate ? (
            <Button onClick={() => navigate(ROUTES.PRODUCTS.CREATE)}>
              <Plus className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('products.create', 'Create Product')}
            </Button>
          ) : undefined
        }
      />

      <ListToolbar
        search={{
          value: (list.filters.searchTerm as string) ?? '',
          onChange: (v) => list.setFilter('searchTerm', v),
        }}
        filters={
          <>
            <Select
              value={(list.filters.status as string) ?? 'all'}
              onValueChange={(v) => list.setFilter('status', v === 'all' ? '' : v)}
            >
              <SelectTrigger className="w-[140px]">
                <SelectValue placeholder={t('products.allStatuses', 'All Statuses')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t('products.allStatuses', 'All Statuses')}</SelectItem>
                <SelectItem value="Draft">{t('products.status.draft', 'Draft')}</SelectItem>
                <SelectItem value="Active">{t('products.status.active', 'Active')}</SelectItem>
                <SelectItem value="Archived">{t('products.status.archived', 'Archived')}</SelectItem>
              </SelectContent>
            </Select>
            {isPlatformAdmin && (
              <Select
                value={(list.filters.tenantId as string) ?? 'all'}
                onValueChange={(v) => list.setFilter('tenantId', v === 'all' ? '' : v)}
              >
                <SelectTrigger className="w-[180px]">
                  <SelectValue placeholder={t('products.allTenants', 'All Tenants')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('products.allTenants', 'All Tenants')}</SelectItem>
                  {tenants.map((tenant: { id: string; name: string }) => (
                    <SelectItem key={tenant.id} value={tenant.id}>
                      {tenant.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </>
        }
      />

      <ListPageState
        isInitialLoading={list.isInitialLoading}
        isError={list.isError}
        isEmpty={list.isEmpty}
        emptyState={{
          icon: Package,
          title: t('products.empty.title', 'No products found'),
          description: t('products.empty.description', 'Create your first product to get started.'),
          action: canCreate
            ? { label: t('products.create', 'Create Product'), onClick: () => navigate(ROUTES.PRODUCTS.CREATE) }
            : undefined,
        }}
      >
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('products.name', 'Name')}</TableHead>
              {isPlatformAdmin && <TableHead>{t('common.tenant', 'Tenant')}</TableHead>}
              <TableHead>{t('products.slug', 'Slug')}</TableHead>
              <TableHead>{t('products.price', 'Price')}</TableHead>
              <TableHead>{t('products.status.label', 'Status')}</TableHead>
              <TableHead>{t('common.createdAt', 'Created')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list.data.map((product) => (
              <TableRow key={product.id}>
                <TableCell>
                  <Link
                    to={ROUTES.PRODUCTS.getDetail(product.id)}
                    className="flex items-center gap-3 font-medium text-primary hover:underline"
                  >
                    <ProductThumbnail imageFileId={product.imageFileId} name={product.name} />
                    {product.name}
                  </Link>
                </TableCell>
                {isPlatformAdmin && (
                  <TableCell className="text-muted-foreground">
                    {product.tenantName ?? '—'}
                  </TableCell>
                )}
                <TableCell className="text-muted-foreground">{product.slug}</TableCell>
                <TableCell>
                  {product.price.toFixed(2)} {product.currency}
                </TableCell>
                <TableCell>
                  <Badge variant={STATUS_BADGE_VARIANT[product.status] ?? 'secondary'}>
                    {product.status}
                  </Badge>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {new Date(product.createdAt).toLocaleDateString()}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        {list.pagination && (
          <Pagination
            pagination={list.pagination}
            onPageChange={list.setPage}
            onPageSizeChange={list.setPageSize}
          />
        )}
      </ListPageState>
    </div>
  );
}

function ProductThumbnail({ imageFileId, name }: { imageFileId?: string; name: string }) {
  const { data: imageUrl } = useFileUrl(imageFileId ?? '');

  return (
    <div className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-muted">
      {imageUrl ? (
        <img src={imageUrl} alt={name} className="h-full w-full object-cover" />
      ) : (
        <ImageIcon className="h-4 w-4 text-muted-foreground/40" />
      )}
    </div>
  );
}
