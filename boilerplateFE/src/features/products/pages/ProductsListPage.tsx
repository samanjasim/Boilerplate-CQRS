import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ImageIcon, Package, Plus, Search } from 'lucide-react';
import { PageHeader, Pagination, EmptyState } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ROUTES } from '@/config';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { useAuthStore, selectUser } from '@/stores';
import { useTenants } from '@/features/tenants/api';
import { useProducts } from '../api';
import { useFileUrl } from '@/features/files/api';
import type { Product } from '@/types';

const STATUS_VARIANTS: Record<string, 'default' | 'secondary' | 'outline'> = {
  Active: 'default',
  Draft: 'secondary',
  Archived: 'outline',
};

export default function ProductsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);
  const isPlatformAdmin = !user?.tenantId;

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize());
  const [searchTerm, setSearchTerm] = useState('');
  const [status, setStatus] = useState<string>('');
  const [tenantFilter, setTenantFilter] = useState<string>('');

  const { data: tenantsData } = useTenants(isPlatformAdmin ? { pageSize: 100 } : undefined);
  const tenants = tenantsData?.data ?? [];

  const { data, isLoading } = useProducts({
    pageNumber,
    pageSize,
    ...(searchTerm && { searchTerm }),
    ...(status && { status }),
    ...(tenantFilter && { tenantId: tenantFilter }),
  });

  const products: Product[] = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('products.title', 'Products')}
        subtitle={t('products.subtitle', 'Manage your product catalog')}
        actions={
          hasPermission(PERMISSIONS.Products.Create) ? (
            <Button onClick={() => navigate(ROUTES.PRODUCTS.CREATE)}>
              <Plus className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('products.create', 'Create Product')}
            </Button>
          ) : undefined
        }
      />

      <div className="flex items-center gap-3">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute ltr:left-3 rtl:right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder={t('common.search', 'Search...')}
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPageNumber(1);
            }}
            className="ltr:pl-9 rtl:pr-9"
          />
        </div>
        <Select
          value={status}
          onValueChange={(v) => {
            setStatus(v === 'all' ? '' : v);
            setPageNumber(1);
          }}
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
            value={tenantFilter}
            onValueChange={(v) => {
              setTenantFilter(v === 'all' ? '' : v);
              setPageNumber(1);
            }}
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
      </div>

      {products.length === 0 && !isLoading ? (
        <EmptyState
          icon={Package}
          title={t('products.empty.title', 'No products found')}
          description={t('products.empty.description', 'Create your first product to get started.')}
          action={
            hasPermission(PERMISSIONS.Products.Create)
              ? { label: t('products.create', 'Create Product'), onClick: () => navigate(ROUTES.PRODUCTS.CREATE) }
              : undefined
          }
        />
      ) : (
        <>
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
              {products.map((product) => (
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
                    <Badge variant={STATUS_VARIANTS[product.status] ?? 'secondary'}>
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

          {pagination && (
            <Pagination
              pagination={pagination}
              onPageChange={setPageNumber}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPageNumber(1);
              }}
            />
          )}
        </>
      )}
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
