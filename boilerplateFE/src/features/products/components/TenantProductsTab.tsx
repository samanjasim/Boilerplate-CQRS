import { useState } from 'react';
import { Package } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { STATUS_BADGE_VARIANT } from '@/constants';
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
    return (
      <div className="flex items-center justify-center py-8">
        <Spinner size="md" />
      </div>
    );
  }

  const products: Product[] = data?.data ?? [];
  const pagination = data?.pagination;

  if (products.length === 0 && pageNumber === 1) {
    return (
      <EmptyState
        icon={Package}
        title={t('products.empty.title')}
        description={t('products.empty.tenantDescription')}
      />
    );
  }

  return (
    <div className="space-y-4">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('products.name')}</TableHead>
            <TableHead>{t('products.price')}</TableHead>
            <TableHead>{t('products.status.label')}</TableHead>
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
                <Badge variant={STATUS_BADGE_VARIANT[product.status] ?? 'secondary'}>
                  {t(`products.status.${product.status.toLowerCase()}`, product.status)}
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
