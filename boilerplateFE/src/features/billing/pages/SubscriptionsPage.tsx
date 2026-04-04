import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { CreditCard } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, Pagination, getPersistedPageSize } from '@/components/common';
import { useAllSubscriptions } from '../api';
import { InlinePlanSelector } from '../components/InlinePlanSelector';
import { ROUTES } from '@/config';
import { formatDate } from '@/utils/format';
import { STATUS_BADGE_VARIANT } from '@/constants';
import { SUBSCRIPTION_STATUS, BILLING_INTERVAL } from '../constants/status';
import type { SubscriptionSummary } from '@/types';

function formatStorage(mb: number): string {
  if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`;
  return `${mb} MB`;
}

export default function SubscriptionsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [searchTerm, setSearchTerm] = useState('');

  const params = useMemo(() => {
    const p: Record<string, unknown> = { pageNumber, pageSize };
    if (searchTerm) p.searchTerm = searchTerm;
    return p;
  }, [pageNumber, pageSize, searchTerm]);

  const { data, isLoading, isFetching, isError } = useAllSubscriptions(params);

  const subscriptions: SubscriptionSummary[] = data?.data ?? [];
  const pagination = data?.pagination;

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('billing.subscriptions')} />
        <EmptyState icon={CreditCard} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('billing.subscriptions')}
        subtitle={t('billing.subscriptionsSubtitle')}
      />

      {/* Search */}
      <div className="max-w-sm">
        <Input
          placeholder={t('common.search')}
          value={searchTerm}
          onChange={(e) => { setSearchTerm(e.target.value); setPageNumber(1); }}
        />
      </div>

      {subscriptions.length === 0 ? (
        <EmptyState icon={CreditCard} title={t('billing.noSubscriptions')} />
      ) : (
        <div className={`relative transition-opacity ${isFetching && !isLoading ? 'opacity-60' : ''}`}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('billing.tenantColumn')}</TableHead>
                <TableHead>{t('billing.planColumn')}</TableHead>
                <TableHead>{t('common.status')}</TableHead>
                <TableHead>{t('billing.usersColumn')}</TableHead>
                <TableHead>{t('billing.storageColumn')}</TableHead>
                <TableHead>{t('billing.webhooksColumn')}</TableHead>
                <TableHead>{t('billing.intervalColumn')}</TableHead>
                <TableHead>{t('billing.renewalColumn')}</TableHead>
                <TableHead>{t('billing.paymentColumn')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {subscriptions.map((row) => (
                <TableRow
                  key={row.tenantId}
                  className="cursor-pointer"
                  onClick={() => navigate(ROUTES.SUBSCRIPTIONS.getDetail(row.tenantId))}
                >
                  <TableCell>
                    <div>
                      <p className="font-medium text-foreground">{row.tenantName}</p>
                      {row.tenantSlug && (
                        <p className="text-xs text-muted-foreground">{row.tenantSlug}</p>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <InlinePlanSelector
                      currentPlanId={row.subscriptionPlanId}
                      tenantId={row.tenantId}
                      tenantName={row.tenantName}
                    />
                  </TableCell>
                  <TableCell>
                    <Badge variant={STATUS_BADGE_VARIANT[SUBSCRIPTION_STATUS[row.status] ?? row.status] ?? 'secondary'}>
                      {SUBSCRIPTION_STATUS[row.status] ?? row.status}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-foreground">
                    {row.usersCount}/{row.maxUsers}
                  </TableCell>
                  <TableCell className="text-foreground">
                    {row.maxStorageMb > 0
                      ? `${formatStorage(row.storageUsedMb)} / ${formatStorage(row.maxStorageMb)}`
                      : '—'}
                  </TableCell>
                  <TableCell className="text-foreground">
                    {row.webhooksCount}/{row.maxWebhooks}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {BILLING_INTERVAL[row.billingInterval] ?? row.billingInterval}
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {formatDate(row.currentPeriodEnd)}
                  </TableCell>
                  <TableCell>
                    {row.latestPaymentStatus ? (
                      <Badge variant={STATUS_BADGE_VARIANT[row.latestPaymentStatus] ?? 'secondary'}>
                        {row.latestPaymentStatus}
                      </Badge>
                    ) : (
                      <Badge variant="outline">{t('billing.noPaymentStatus')}</Badge>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}
    </div>
  );
}
