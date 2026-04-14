import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Webhook, ChevronDown, ChevronUp } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { PageHeader, EmptyState, Pagination, getPersistedPageSize } from '@/components/common';
import { useWebhookAdminEndpoints, useWebhookAdminDeliveries } from '../api';
import { useBackNavigation, useTimeAgo } from '@/hooks';
import { ROUTES } from '@/config';
import { statusBadge } from '../utils/badges';
import { tryPrettyJson } from '../utils/format';
import type { WebhookAdminSummary, WebhookDelivery, PaginationMeta } from '@/types';

type DeliveryStatus = 'All' | 'Success' | 'Failed' | 'Pending';

function DeliveryRow({ delivery }: { delivery: WebhookDelivery }) {
  const [expanded, setExpanded] = useState(false);
  const { t } = useTranslation();

  const timeAgo = useTimeAgo(delivery.createdAt);

  return (
    <>
      <TableRow
        className="cursor-pointer hover:bg-secondary/50 transition-colors"
        onClick={() => setExpanded((p) => !p)}
      >
        <TableCell className="text-muted-foreground text-sm whitespace-nowrap">
          {timeAgo}
        </TableCell>
        <TableCell>
          <code className="rounded-md bg-secondary px-2 py-0.5 text-xs">{delivery.eventType}</code>
        </TableCell>
        <TableCell>{statusBadge(delivery.status)}</TableCell>
        <TableCell className="text-muted-foreground text-sm">
          {delivery.responseStatusCode ?? '—'}
        </TableCell>
        <TableCell className="text-muted-foreground text-sm">
          {delivery.duration != null ? `${delivery.duration} ms` : '—'}
        </TableCell>
        <TableCell className="text-muted-foreground text-sm text-end">
          {delivery.attemptCount}
        </TableCell>
        <TableCell className="text-end">
          {expanded ? (
            <ChevronUp className="h-4 w-4 text-muted-foreground ml-auto" />
          ) : (
            <ChevronDown className="h-4 w-4 text-muted-foreground ml-auto" />
          )}
        </TableCell>
      </TableRow>

      {expanded && (
        <TableRow className="bg-secondary/30">
          <TableCell colSpan={7} className="p-4">
            <div className="space-y-3 text-sm">
              {delivery.requestPayload && (
                <div>
                  <p className="text-xs font-semibold text-muted-foreground mb-1 uppercase tracking-wide">
                    {t('webhooks.requestPayload')}
                  </p>
                  <pre className="rounded-lg bg-card border border-border p-3 text-xs font-mono overflow-x-auto whitespace-pre-wrap break-all text-foreground">
                    {tryPrettyJson(delivery.requestPayload)}
                  </pre>
                </div>
              )}
              {delivery.responseBody && (
                <div>
                  <p className="text-xs font-semibold text-muted-foreground mb-1 uppercase tracking-wide">
                    {t('webhooks.responseBody')}
                  </p>
                  <pre className="rounded-lg bg-card border border-border p-3 text-xs font-mono overflow-x-auto whitespace-pre-wrap break-all text-foreground">
                    {tryPrettyJson(delivery.responseBody)}
                  </pre>
                </div>
              )}
              {delivery.errorMessage && (
                <div>
                  <p className="text-xs font-semibold text-destructive mb-1 uppercase tracking-wide">
                    {t('webhooks.errorMessage')}
                  </p>
                  <p className="rounded-lg bg-destructive/10 border border-destructive/20 p-3 text-xs text-destructive font-mono">
                    {delivery.errorMessage}
                  </p>
                </div>
              )}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

export default function WebhookAdminDetailPage() {
  const { t } = useTranslation();
  const { endpointId } = useParams<{ endpointId: string }>();
  useBackNavigation(ROUTES.WEBHOOKS_ADMIN.LIST, t('webhooks.adminTitle'));

  const [statusFilter, setStatusFilter] = useState<DeliveryStatus>('All');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);

  const deliveryParams: Record<string, unknown> = { pageNumber, pageSize };
  if (statusFilter !== 'All') deliveryParams.status = statusFilter;

  // Find endpoint info from admin list (lightweight — avoids extra API)
  const { data: endpointsData } = useWebhookAdminEndpoints({ pageSize: 100 });
  const endpoint: WebhookAdminSummary | undefined = (endpointsData?.data ?? []).find(
    (e: WebhookAdminSummary) => e.id === endpointId,
  );

  const { data: deliveriesData, isLoading: deliveriesLoading } = useWebhookAdminDeliveries(
    endpointId ?? '',
    deliveryParams,
  );

  const deliveries: WebhookDelivery[] = deliveriesData?.data ?? [];
  const pagination: PaginationMeta | undefined = deliveriesData?.pagination;

  const headerTitle = endpoint
    ? t('webhooks.endpointDetail')
    : t('webhooks.endpointDetail');

  return (
    <div className="space-y-6">
      <PageHeader title={headerTitle} />

      {/* Endpoint Info Card */}
      {endpoint && (
        <Card>
          <CardContent className="py-5">
            <div className="space-y-3">
              <div className="flex items-center gap-3 flex-wrap">
                <h3 className="text-lg font-bold text-foreground">{endpoint.tenantName}</h3>
                {endpoint.isActive ? (
                  <Badge variant="default">{t('webhooks.active')}</Badge>
                ) : (
                  <Badge variant="secondary">{t('webhooks.inactive')}</Badge>
                )}
              </div>
              <code className="text-sm font-mono text-muted-foreground break-all">{endpoint.url}</code>
              {endpoint.description && (
                <p className="text-sm text-muted-foreground">{endpoint.description}</p>
              )}
              <div className="flex flex-wrap gap-1">
                {endpoint.events.map((ev) => (
                  <Badge key={ev} variant="outline" className="text-xs">
                    {ev}
                  </Badge>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Delivery History */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">{t('webhooks.deliveries')}</h2>

        {/* Filter bar */}
        <div className="flex items-center gap-3">
          <Select
            value={statusFilter}
            onValueChange={(v) => {
              setStatusFilter(v as DeliveryStatus);
              setPageNumber(1);
            }}
          >
            <SelectTrigger className="w-[140px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="All">{t('webhooks.allEvents')}</SelectItem>
              <SelectItem value="Success">{t('webhooks.statusSuccess')}</SelectItem>
              <SelectItem value="Failed">{t('webhooks.statusFailed')}</SelectItem>
              <SelectItem value="Pending">{t('webhooks.statusPending')}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {deliveriesLoading ? (
          <div className="flex justify-center py-12">
            <Spinner size="lg" />
          </div>
        ) : deliveries.length === 0 ? (
          <EmptyState
            icon={Webhook}
            title={t('webhooks.noDeliveries')}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('common.createdAt')}</TableHead>
                <TableHead>{t('webhooks.eventType')}</TableHead>
                <TableHead>{t('webhooks.deliveryStatus')}</TableHead>
                <TableHead>{t('webhooks.responseCode')}</TableHead>
                <TableHead>{t('webhooks.duration')}</TableHead>
                <TableHead className="text-end">{t('webhooks.attempts')}</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {deliveries.map((delivery) => (
                <DeliveryRow key={delivery.id} delivery={delivery} />
              ))}
            </TableBody>
          </Table>
        )}

        {pagination && (
          <Pagination
            pagination={pagination}
            onPageChange={setPageNumber}
            onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
          />
        )}
      </section>
    </div>
  );
}
