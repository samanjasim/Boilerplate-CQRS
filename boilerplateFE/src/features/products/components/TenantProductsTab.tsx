import { useState } from 'react';
import { Package } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { useProducts } from '../api';
import type { Product } from '@/types';

interface TenantProductsTabProps {
  tenantId: string;
}

export function TenantProductsTab({ tenantId }: TenantProductsTabProps) {
  const { t } = useTranslation();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize());

  const { data, isLoading } = useProducts({ pageNumber, pageSize, tenantId });

  if (isLoading) {
    return <div className="flex items-center justify-center py-8 text-muted-foreground">Loading...</div>;
  }

  const products: Product[] = data?.data ?? [];
  const pagination = data?.pagination;

  if (products.length === 0 && pageNumber === 1) {
    return (
      <EmptyState
        icon={Package}
        title={t('products.empty.title', 'No products found')}
        description={t('products.empty.tenantDescription', 'This tenant has no products yet.')}
      />
    );
  }

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">{t('products.title', 'Products')}</h3>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('products.name', 'Name')}</TableHead>
            <TableHead>{t('products.price', 'Price')}</TableHead>
            <TableHead>{t('products.status.label', 'Status')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {products.map((product) => (
            <TableRow key={product.id}>
              <TableCell className="font-medium">{product.name}</TableCell>
              <TableCell>
                {product.price.toFixed(2)} {product.currency}
              </TableCell>
              <TableCell>
                <Badge
                  variant={
                    product.status === 'Active' ? 'default' : product.status === 'Draft' ? 'secondary' : 'outline'
                  }
                >
                  {product.status}
                </Badge>
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
    </div>
  );
}
