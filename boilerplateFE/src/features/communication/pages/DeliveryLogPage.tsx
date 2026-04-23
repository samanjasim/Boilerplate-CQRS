import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { format } from 'date-fns';
import { Send, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { usePermissions } from '@/hooks';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import { useDeliveryLogs, useResendDelivery } from '../api';
import { DeliveryDetailModal } from '../components/DeliveryDetailModal';
import type { DeliveryLogDto } from '@/types/communication.types';

export default function DeliveryLogPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canResend = hasPermission(PERMISSIONS.Communication.Resend);

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize());
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [channelFilter, setChannelFilter] = useState<string>('');
  const [templateFilter, setTemplateFilter] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const { data, isLoading } = useDeliveryLogs({
    pageNumber,
    pageSize,
    ...(statusFilter && { status: statusFilter }),
    ...(channelFilter && { channel: channelFilter }),
    ...(templateFilter && { templateName: templateFilter }),
  });

  const resendMutation = useResendDelivery();

  const logs: DeliveryLogDto[] = data?.data ?? [];
  const pagination = data?.pagination;

  const formatDuration = (ms: number | null) => {
    if (ms === null) return '-';
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('communication.deliveryLog.title')}
        subtitle={t('communication.deliveryLog.subtitle')}
      />

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={statusFilter}
          onValueChange={(v) => {
            setStatusFilter(v === 'all' ? '' : v);
            setPageNumber(1);
          }}
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder={t('communication.deliveryLog.filters.allStatuses')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('communication.deliveryLog.filters.allStatuses')}</SelectItem>
            {['Pending', 'Queued', 'Sending', 'Delivered', 'Failed', 'Bounced'].map((s) => (
              <SelectItem key={s} value={s}>
                {t(`communication.deliveryLog.statusLabels.${s}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={channelFilter}
          onValueChange={(v) => {
            setChannelFilter(v === 'all' ? '' : v);
            setPageNumber(1);
          }}
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder={t('communication.deliveryLog.filters.allChannels')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('communication.deliveryLog.filters.allChannels')}</SelectItem>
            {['Email', 'Sms', 'Push', 'WhatsApp', 'InApp'].map((ch) => (
              <SelectItem key={ch} value={ch}>
                {t(`communication.channels.channelNames.${ch}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Input
          placeholder={t('communication.deliveryLog.filters.template')}
          value={templateFilter}
          onChange={(e) => {
            setTemplateFilter(e.target.value);
            setPageNumber(1);
          }}
          className="max-w-[200px]"
        />
      </div>

      {isLoading ? (
        <div className="flex justify-center py-12"><Spinner /></div>
      ) : logs.length === 0 ? (
        <EmptyState
          icon={Send}
          title={t('communication.deliveryLog.noDeliveries')}
          description={t('communication.deliveryLog.noDeliveriesDescription')}
        />
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('communication.deliveryLog.columns.timestamp')}</TableHead>
                <TableHead>{t('communication.deliveryLog.columns.recipient')}</TableHead>
                <TableHead>{t('communication.deliveryLog.columns.template')}</TableHead>
                <TableHead>{t('communication.deliveryLog.columns.channel')}</TableHead>
                <TableHead>{t('communication.deliveryLog.columns.status')}</TableHead>
                <TableHead>{t('communication.deliveryLog.columns.duration')}</TableHead>
                <TableHead className="text-center">{t('communication.deliveryLog.columns.attempts')}</TableHead>
                <TableHead className="text-end">{t('communication.deliveryLog.columns.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {logs.map((log) => (
                <TableRow
                  key={log.id}
                  className="cursor-pointer"
                  onClick={() => setSelectedId(log.id)}
                >
                  <TableCell className="whitespace-nowrap text-sm">
                    {format(new Date(log.createdAt), 'MMM d, yyyy HH:mm:ss')}
                  </TableCell>
                  <TableCell className="max-w-[200px] truncate text-sm">
                    {log.recipientAddress ?? '-'}
                  </TableCell>
                  <TableCell className="max-w-[180px] truncate text-sm">
                    {log.templateName}
                  </TableCell>
                  <TableCell className="text-sm">
                    {log.channel
                      ? t(`communication.channels.channelNames.${log.channel}`)
                      : log.integrationType ?? '-'}
                  </TableCell>
                  <TableCell>
                    <Badge variant={STATUS_BADGE_VARIANT[log.status] ?? 'outline'}>
                      {t(`communication.deliveryLog.statusLabels.${log.status}`)}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatDuration(log.totalDurationMs)}
                  </TableCell>
                  <TableCell className="text-center text-sm">
                    {log.attemptCount}
                  </TableCell>
                  <TableCell className="text-end">
                    {canResend && (log.status === 'Failed' || log.status === 'Bounced') && (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={resendMutation.isPending}
                        onClick={(e) => {
                          e.stopPropagation();
                          resendMutation.mutate(log.id);
                        }}
                      >
                        <RefreshCw className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                        {t('communication.deliveryLog.resend')}
                      </Button>
                    )}
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

      {selectedId && (
        <DeliveryDetailModal
          id={selectedId}
          open={!!selectedId}
          onOpenChange={(open) => {
            if (!open) setSelectedId(null);
          }}
        />
      )}
    </div>
  );
}
