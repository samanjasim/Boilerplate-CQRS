import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Webhook, Plus, Pencil, Trash2, Send, List, KeyRound } from 'lucide-react';
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
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import { useWebhookEndpoints, useDeleteWebhook, useTestWebhook, useRegenerateWebhookSecret } from '../api';
import { CreateWebhookDialog } from '../components/CreateWebhookDialog';
import { EditWebhookDialog } from '../components/EditWebhookDialog';
import { DeliveryLogModal } from '../components/DeliveryLogModal';
import { usePermissions, useTimeAgoFormatter } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { lastStatusBadge } from '../utils/badges';
import { truncateUrl } from '../utils/format';
import type { WebhookEndpoint } from '@/types';

export default function WebhooksPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<WebhookEndpoint | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<WebhookEndpoint | null>(null);
  const [regenerateTarget, setRegenerateTarget] = useState<WebhookEndpoint | null>(null);
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null);
  const [deliveriesTarget, setDeliveriesTarget] = useState<WebhookEndpoint | null>(null);
  const formatTimeAgo = useTimeAgoFormatter();

  const { data, isLoading, isError } = useWebhookEndpoints();
  const deleteMutation = useDeleteWebhook();
  const testMutation = useTestWebhook();
  const regenerateMutation = useRegenerateWebhookSecret();

  const endpoints: WebhookEndpoint[] = data?.data ?? [];

  const handleDelete = async () => {
    if (!deleteTarget) return;
    await deleteMutation.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  };

  const handleTest = (endpoint: WebhookEndpoint) => {
    testMutation.mutate(endpoint.id);
  };

  const handleRegenerate = () => {
    if (!regenerateTarget) return;
    regenerateMutation.mutate(regenerateTarget.id, {
      onSuccess: (secret) => {
        setRegenerateTarget(null);
        setRevealedSecret(secret);
      },
    });
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
                      {lastStatusBadge(endpoint.lastDeliveryStatus)}
                      {endpoint.lastDeliveryAt && (
                        <span className="text-xs text-muted-foreground">
                          {formatTimeAgo(endpoint.lastDeliveryAt)}
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
                        aria-label={t('webhooks.deliveries')}
                        onClick={() => setDeliveriesTarget(endpoint)}
                      >
                        <List className="h-4 w-4" />
                      </Button>

                      {/* Test */}
                      <Button
                        variant="ghost"
                        size="icon"
                        title={t('webhooks.sendTest')}
                        aria-label={t('webhooks.sendTest')}
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
                          aria-label={t('webhooks.editEndpoint')}
                          onClick={() => setEditTarget(endpoint)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                      )}

                      {/* Regenerate secret */}
                      {hasPermission(PERMISSIONS.Webhooks.Update) && (
                        <Button
                          variant="ghost"
                          size="icon"
                          title={t('webhooks.regenerateSecret')}
                          aria-label={t('webhooks.regenerateSecret')}
                          onClick={() => setRegenerateTarget(endpoint)}
                        >
                          <KeyRound className="h-4 w-4" />
                        </Button>
                      )}

                      {/* Delete */}
                      {hasPermission(PERMISSIONS.Webhooks.Delete) && (
                        <Button
                          variant="ghost"
                          size="icon"
                          title={t('webhooks.deleteEndpoint')}
                          aria-label={t('webhooks.deleteEndpoint')}
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

      <ConfirmDialog
        isOpen={!!regenerateTarget}
        onClose={() => setRegenerateTarget(null)}
        title={t('webhooks.regenerateSecret')}
        description={t('webhooks.regenerateSecretConfirm')}
        confirmLabel={t('webhooks.regenerateSecret')}
        onConfirm={handleRegenerate}
        isLoading={regenerateMutation.isPending}
        variant="danger"
      />

      <Dialog
        open={!!revealedSecret}
        onOpenChange={(open) => !open && setRevealedSecret(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('webhooks.newSecretTitle')}</DialogTitle>
            <DialogDescription>{t('webhooks.newSecretBody')}</DialogDescription>
          </DialogHeader>
          <code className="block w-full break-all rounded-lg bg-muted p-3 text-xs">
            {revealedSecret}
          </code>
          <DialogFooter>
            <Button onClick={() => setRevealedSecret(null)}>
              {t('common.close')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
