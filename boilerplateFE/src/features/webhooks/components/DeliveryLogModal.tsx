import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDistanceToNow } from 'date-fns';
import { ChevronDown, ChevronUp } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Pagination, getPersistedPageSize } from '@/components/common';
import { useWebhookDeliveries } from '../api';
import type { WebhookEndpoint, WebhookDelivery, PaginationMeta } from '@/types';

interface DeliveryLogModalProps {
  endpoint: WebhookEndpoint | null;
  onOpenChange: (open: boolean) => void;
}

type DeliveryStatus = 'All' | 'Success' | 'Failed' | 'Pending';

function statusBadge(status: WebhookDelivery['status']) {
  switch (status) {
    case 'Success':
      return (
        <Badge className="bg-success/10 text-success border-0 font-medium">
          {status}
        </Badge>
      );
    case 'Failed':
      return <Badge variant="destructive">{status}</Badge>;
    case 'Pending':
      return (
        <Badge className="bg-warning/10 text-warning border-0 font-medium">
          {status}
        </Badge>
      );
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}

function DeliveryRow({ delivery }: { delivery: WebhookDelivery }) {
  const [expanded, setExpanded] = useState(false);
  const { t } = useTranslation();

  const timeAgo = formatDistanceToNow(new Date(delivery.createdAt), { addSuffix: true });

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

function tryPrettyJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

export function DeliveryLogModal({ endpoint, onOpenChange }: DeliveryLogModalProps) {
  const { t } = useTranslation();
  const [statusFilter, setStatusFilter] = useState<DeliveryStatus>('All');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(getPersistedPageSize);

  const params: Record<string, unknown> = { pageNumber, pageSize };
  if (statusFilter !== 'All') params.status = statusFilter;

  const { data, isLoading } = useWebhookDeliveries(endpoint?.id ?? '', params);

  const deliveries: WebhookDelivery[] = data?.data ?? [];
  const pagination: PaginationMeta | undefined = data?.pagination;

  const handleClose = () => {
    setStatusFilter('All');
    setPageNumber(1);
    onOpenChange(false);
  };

  return (
    <Dialog open={!!endpoint} onOpenChange={(open) => !open && handleClose()}>
      <DialogContent className="sm:max-w-4xl max-h-[90vh] flex flex-col overflow-hidden">
        <DialogHeader>
          <DialogTitle>{t('webhooks.deliveries')}</DialogTitle>
          <DialogDescription className="truncate max-w-lg">
            {endpoint?.url}
          </DialogDescription>
        </DialogHeader>

        {/* Filter bar */}
        <div className="flex items-center gap-3 shrink-0">
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

        {/* Table */}
        <div className="flex-1 overflow-y-auto min-h-0">
          {isLoading ? (
            <div className="flex justify-center py-12">
              <Spinner size="lg" />
            </div>
          ) : deliveries.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-center">
              <p className="text-sm text-muted-foreground">{t('webhooks.noDeliveries')}</p>
            </div>
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
        </div>

        {pagination && (
          <Pagination
            pagination={pagination}
            onPageChange={setPageNumber}
            className="shrink-0"
          />
        )}

        <div className="flex justify-end shrink-0 pt-2">
          <Button variant="outline" onClick={handleClose}>
            {t('common.close')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
