import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { formatDistanceToNow } from 'date-fns';
import { Webhook, Globe, CheckCircle, XCircle, Activity } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, Pagination, getPersistedPageSize } from '@/components/common';
import { useWebhookAdminEndpoints, useWebhookAdminStats } from '../api';
import { ROUTES } from '@/config';
import type { WebhookAdminSummary } from '@/types';

function truncateUrl(url: string, maxLen = 40): string {
  if (url.length <= maxLen) return url;
  return `${url.slice(0, maxLen)}...`;
}

function lastStatusBadge(status: string | null) {
  if (!status) return <span className="text-muted-foreground">—</span>;
  switch (status) {
    case 'Success':
      return (
        <Badge className="bg-success/10 text-success border-0 font-medium text-xs">
          {status}
        </Badge>
      );
    case 'Failed':
      return <Badge variant="destructive" className="text-xs">{status}</Badge>;
    case 'Pending':
      return (
        <Badge className="bg-warning/10 text-warning border-0 font-medium text-xs">
          {status}
        </Badge>
      );
    default:
      return <Badge variant="secondary" className="text-xs">{status}</Badge>;
  }
}

function StatCard({
  icon: Icon,
  label,
  value,
  subtitle,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: string | number;
  subtitle?: string;
  color: 'primary' | 'accent' | 'success' | 'info';
}) {
  const colors = {
    primary: '[background:var(--active-bg)] [color:var(--active-text)]',
    accent: 'bg-accent-500/10 text-accent-600',
    success: 'bg-green-500/10 text-green-600',
    info: 'bg-blue-500/10 text-blue-600',
  };

  return (
    <Card className="hover-lift">
      <CardContent className="py-6">
        <div className="flex items-center gap-4">
          <div className={`flex h-11 w-11 items-center justify-center rounded-xl ${colors[color]}`}>
            <Icon className="h-6 w-6" />
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            <p className="text-2xl font-bold text-foreground">{value}</p>
            {subtitle && <p className="text-xs text-muted-foreground">{subtitle}</p>}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default function WebhookAdminPage() {
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

  const { data, isLoading, isFetching, isError } = useWebhookAdminEndpoints(params);
  const { data: stats } = useWebhookAdminStats();

  const endpoints: WebhookAdminSummary[] = data?.data ?? [];
  const pagination = data?.pagination;

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('webhooks.adminTitle')} />
        <EmptyState icon={Webhook} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
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
        title={t('webhooks.adminTitle')}
        subtitle={t('webhooks.adminSubtitle')}
      />

      {/* Stats Cards */}
      {stats && (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <StatCard
            icon={Globe}
            label={t('webhooks.totalEndpoints')}
            value={stats.totalEndpoints}
            subtitle={`${stats.activeEndpoints} ${t('webhooks.activeEndpoints')}`}
            color="primary"
          />
          <StatCard
            icon={Activity}
            label={t('webhooks.deliveries24h')}
            value={stats.totalDeliveries24h}
            color="accent"
          />
          <StatCard
            icon={CheckCircle}
            label={t('webhooks.successRate')}
            value={`${stats.successRate24h.toFixed(1)}%`}
            color="success"
          />
          <StatCard
            icon={XCircle}
            label={t('webhooks.failed24h')}
            value={stats.failedDeliveries24h}
            color="info"
          />
        </div>
      )}

      {/* Search */}
      <div className="max-w-sm">
        <Input
          placeholder={t('common.search')}
          value={searchTerm}
          onChange={(e) => { setSearchTerm(e.target.value); setPageNumber(1); }}
        />
      </div>

      {endpoints.length === 0 ? (
        <EmptyState icon={Webhook} title={t('webhooks.noEndpoints')} />
      ) : (
        <div className={`relative transition-opacity ${isFetching && !isLoading ? 'opacity-60' : ''}`}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('webhooks.tenantColumn')}</TableHead>
                <TableHead>{t('webhooks.urlColumn')}</TableHead>
                <TableHead>{t('webhooks.eventsColumn')}</TableHead>
                <TableHead>{t('common.status')}</TableHead>
                <TableHead>{t('webhooks.deliveriesColumn')}</TableHead>
                <TableHead>{t('webhooks.successRateColumn')}</TableHead>
                <TableHead>{t('webhooks.lastDeliveryColumn')}</TableHead>
                <TableHead>{t('webhooks.lastStatusColumn')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {endpoints.map((row) => {
                const visibleEvents = row.events.slice(0, 2);
                const extraCount = row.events.length - visibleEvents.length;
                const successRate = row.deliveriesLast24h > 0
                  ? `${((row.successfulLast24h / row.deliveriesLast24h) * 100).toFixed(0)}%`
                  : '—';

                return (
                  <TableRow
                    key={row.id}
                    className="cursor-pointer"
                    onClick={() => navigate(ROUTES.WEBHOOKS_ADMIN.getDetail(row.id))}
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
                      <code className="text-xs font-mono text-muted-foreground" title={row.url}>
                        {truncateUrl(row.url)}
                      </code>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {visibleEvents.map((ev) => (
                          <Badge key={ev} variant="outline" className="text-xs">
                            {ev}
                          </Badge>
                        ))}
                        {extraCount > 0 && (
                          <Badge variant="secondary" className="text-xs">
                            +{extraCount}
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      {row.isActive ? (
                        <Badge variant="default">{t('webhooks.active')}</Badge>
                      ) : (
                        <Badge variant="secondary">{t('webhooks.inactive')}</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-foreground">
                      {row.deliveriesLast24h}
                    </TableCell>
                    <TableCell className="text-foreground">
                      {successRate}
                    </TableCell>
                    <TableCell className="text-muted-foreground text-sm">
                      {row.lastDeliveryAt
                        ? formatDistanceToNow(new Date(row.lastDeliveryAt), { addSuffix: true })
                        : t('webhooks.never')}
                    </TableCell>
                    <TableCell>
                      {lastStatusBadge(row.lastDeliveryStatus)}
                    </TableCell>
                  </TableRow>
                );
              })}
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
