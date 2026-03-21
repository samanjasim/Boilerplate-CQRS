import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState } from '@/components/common';
import { useTenants } from '../api';
import { ROUTES } from '@/config';
import { Building } from 'lucide-react';
import { format } from 'date-fns';

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Pending: 'secondary',
  Suspended: 'destructive',
  Deactivated: 'destructive',
};

export default function TenantsListPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useTenants();

  const tenants = data?.data ?? [];

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('tenants.title')} />
        <EmptyState icon={Building} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('tenants.title')} subtitle={t('tenants.allTenants')} />

      {tenants.length === 0 ? (
        <EmptyState icon={Building} title={t('tenants.noTenants')} />
      ) : (
        <Card>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('tenants.name')}</TableHead>
                  <TableHead>{t('tenants.slug')}</TableHead>
                  <TableHead>{t('tenants.status')}</TableHead>
                  <TableHead>{t('common.createdAt')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {tenants.map((tenant: { id: string; name: string; slug: string | null; status: string; createdAt: string }) => (
                  <TableRow key={tenant.id}>
                    <TableCell>
                      <Link to={ROUTES.TENANTS.getDetail(tenant.id)} className="font-medium text-foreground hover:text-primary">
                        {tenant.name}
                      </Link>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {tenant.slug || '-'}
                    </TableCell>
                    <TableCell>
                      <Badge variant={STATUS_VARIANT[tenant.status] || 'default'}>
                        {tenant.status}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {tenant.createdAt ? format(new Date(tenant.createdAt), 'MMM d, yyyy') : '-'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
