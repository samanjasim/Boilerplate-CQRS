import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDistanceToNow } from 'date-fns';
import { Webhook, Plus, Pencil, Trash2, Send, List } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import { useWebhookEndpoints, useDeleteWebhook, useTestWebhook } from '../api';
import { CreateWebhookDialog } from '../components/CreateWebhookDialog';
import { EditWebhookDialog } from '../components/EditWebhookDialog';
import { DeliveryLogModal } from '../components/DeliveryLogModal';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { WebhookEndpoint } from '@/types';

/** Truncate a URL to ~40 chars while keeping the host readable */
function truncateUrl(url: string, maxLen = 40): string {
  if (url.length <= maxLen) return url;
  return `${url.slice(0, maxLen)}…`;
}

function lastDeliveryBadge(status: string | null) {
  if (!status) return null;
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

export default function WebhooksPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<WebhookEndpoint | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<WebhookEndpoint | null>(null);
  const [deliveriesTarget, setDeliveriesTarget] = useState<WebhookEndpoint | null>(null);

  const { data, isLoading, isError } = useWebhookEndpoints();
  const deleteMutation = useDeleteWebhook();
  const testMutation = useTestWebhook();

  const endpoints: WebhookEndpoint[] = data?.data ?? [];

  const handleDelete = async () => {
    if (!deleteTarget) return;
    await deleteMutation.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  };

  const handleTest = (endpoint: WebhookEndpoint) => {
    testMutation.mutate(endpoint.id);
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('webhooks.title')} />
        <EmptyState
          icon={Webhook}
          title={t('common.errorOccurred')}
          description={t('common.tryAgain')}
        />
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
        title={t('webhooks.title')}
        subtitle={t('webhooks.subtitle')}
        actions={
          hasPermission(PERMISSIONS.Webhooks.Create) ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              {t('webhooks.addEndpoint')}
            </Button>
          ) : undefined
        }
      />

      {endpoints.length === 0 ? (
        <EmptyState
          icon={Webhook}
          title={t('webhooks.noEndpoints')}
          description={t('webhooks.noEndpointsDesc')}
          action={
            hasPermission(PERMISSIONS.Webhooks.Create)
              ? { label: t('webhooks.addEndpoint'), onClick: () => setCreateOpen(true) }
              : undefined
          }
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('webhooks.url')}</TableHead>
              <TableHead>{t('webhooks.description')}</TableHead>
              <TableHead>{t('webhooks.events')}</TableHead>
              <TableHead>{t('webhooks.status')}</TableHead>
              <TableHead>{t('webhooks.lastDelivery')}</TableHead>
              <TableHead className="text-end">{t('common.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {endpoints.map((endpoint) => {
              const visibleEvents = endpoint.events.slice(0, 3);
              const extraCount = endpoint.events.length - visibleEvents.length;

              return (
                <TableRow key={endpoint.id}>
                  {/* URL */}
                  <TableCell className="font-medium text-foreground">
                    <code
                      className="text-xs text-muted-foreground"
                      title={endpoint.url}
                    >
                      {truncateUrl(endpoint.url)}
                    </code>
                  </TableCell>

                  {/* Description */}
                  <TableCell className="text-muted-foreground text-sm max-w-[180px] truncate">
                    {endpoint.description ?? '—'}
                  </TableCell>

                  {/* Events */}
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

                  {/* Active */}
                  <TableCell>
                    {endpoint.isActive ? (
                      <Badge variant="default">{t('webhooks.active')}</Badge>
                    ) : (
                      <Badge variant="secondary">{t('webhooks.inactive')}</Badge>
                    )}
                  </TableCell>

                  {/* Last Delivery */}
                  <TableCell>
                    <div className="flex flex-col gap-0.5">
                      {lastDeliveryBadge(endpoint.lastDeliveryStatus)}
                      {endpoint.lastDeliveryAt && (
                        <span className="text-xs text-muted-foreground">
                          {formatDistanceToNow(new Date(endpoint.lastDeliveryAt), {
                            addSuffix: true,
                          })}
                        </span>
                      )}
                      {!endpoint.lastDeliveryAt && !endpoint.lastDeliveryStatus && (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </div>
                  </TableCell>

                  {/* Actions */}
                  <TableCell className="text-end">
                    <div className="flex justify-end gap-1">
                      {/* Deliveries */}
                      <Button
                        variant="ghost"
                        size="icon"
                        title={t('webhooks.deliveries')}
                        onClick={() => setDeliveriesTarget(endpoint)}
                      >
                        <List className="h-4 w-4" />
                      </Button>

                      {/* Test */}
                      <Button
                        variant="ghost"
                        size="icon"
                        title={t('webhooks.sendTest')}
                        onClick={() => handleTest(endpoint)}
                        disabled={testMutation.isPending}
                      >
                        <Send className="h-4 w-4" />
                      </Button>

                      {/* Edit */}
                      {hasPermission(PERMISSIONS.Webhooks.Update) && (
                        <Button
                          variant="ghost"
                          size="icon"
                          title={t('webhooks.editEndpoint')}
                          onClick={() => setEditTarget(endpoint)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                      )}

                      {/* Delete */}
                      {hasPermission(PERMISSIONS.Webhooks.Delete) && (
                        <Button
                          variant="ghost"
                          size="icon"
                          title={t('webhooks.deleteEndpoint')}
                          onClick={() => setDeleteTarget(endpoint)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}

      {/* Dialogs */}
      <CreateWebhookDialog open={createOpen} onOpenChange={setCreateOpen} />

      <EditWebhookDialog endpoint={editTarget} onOpenChange={(open) => !open && setEditTarget(null)} />

      <DeliveryLogModal
        endpoint={deliveriesTarget}
        onOpenChange={(open) => !open && setDeliveriesTarget(null)}
      />

      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title={t('webhooks.deleteEndpoint')}
        description={t('webhooks.deleteConfirm')}
        confirmLabel={t('common.delete')}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
        variant="danger"
      />
    </div>
  );
}
