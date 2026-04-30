import { useTranslation } from 'react-i18next';
import { format } from 'date-fns';
import { RefreshCw, CheckCircle2, XCircle, Clock, AlertTriangle } from 'lucide-react';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetBody,
} from '@/components/ui/sheet';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Spinner } from '@/components/ui/spinner';
import { usePermissions } from '@/hooks';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import { useDeliveryLog, useResendDelivery } from '../api';
import type { DeliveryStatus } from '@/types/communication.types';

const STATUS_ICONS: Record<DeliveryStatus, typeof CheckCircle2> = {
  Pending: Clock,
  Queued: Clock,
  Sending: Clock,
  Delivered: CheckCircle2,
  Failed: XCircle,
  Bounced: AlertTriangle,
};

interface DeliveryDetailDrawerProps {
  id: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DeliveryDetailDrawer({ id, open, onOpenChange }: DeliveryDetailDrawerProps) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canResend = hasPermission(PERMISSIONS.Communication.Resend);

  const { data, isLoading } = useDeliveryLog(id);
  const resendMutation = useResendDelivery();

  const log = data?.data;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="end" width="lg">
        <SheetHeader>
          <SheetTitle>{t('communication.deliveryLog.drawer.title')}</SheetTitle>
          {log && (
            <SheetDescription>
              {t('communication.deliveryLog.drawer.attemptCount', { count: log.attempts.length })}
            </SheetDescription>
          )}
        </SheetHeader>

        <SheetBody>
          {isLoading || !log ? (
            <div className="flex justify-center py-8"><Spinner /></div>
          ) : (
            <div className="space-y-6">
              {/* Summary */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.recipient')}</p>
                  <p className="text-sm font-medium">{log.recipientAddress ?? '-'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.template')}</p>
                  <p className="text-sm font-medium">{log.templateName}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.channel')}</p>
                  <p className="text-sm font-medium">
                    {log.channel
                      ? t(`communication.channels.channelNames.${log.channel}`)
                      : log.integrationType ?? '-'}
                    {log.provider ? ` (${log.provider})` : ''}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.status')}</p>
                  <Badge variant={STATUS_BADGE_VARIANT[log.status]}>
                    {t(`communication.deliveryLog.statusLabels.${log.status}`)}
                  </Badge>
                </div>
                {log.subject && (
                  <div className="col-span-2">
                    <p className="text-sm text-muted-foreground">Subject</p>
                    <p className="text-sm font-medium">{log.subject}</p>
                  </div>
                )}
                {log.bodyPreview && (
                  <div className="col-span-2">
                    <p className="text-sm text-muted-foreground">Body Preview</p>
                    <p className="text-sm text-muted-foreground bg-muted rounded-lg p-3 mt-1 whitespace-pre-wrap max-h-32 overflow-y-auto">
                      {log.bodyPreview}
                    </p>
                  </div>
                )}
                {log.errorMessage && (
                  <div className="col-span-2">
                    <p className="text-sm text-destructive font-medium">Error</p>
                    <p className="text-sm text-destructive/80">{log.errorMessage}</p>
                  </div>
                )}
              </div>

              {/* Resend */}
              {canResend && (log.status === 'Failed' || log.status === 'Bounced') && (
                <Button
                  variant="outline"
                  disabled={resendMutation.isPending}
                  onClick={() => resendMutation.mutate(log.id)}
                >
                  <RefreshCw className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  {t('communication.deliveryLog.resend')}
                </Button>
              )}

              <Separator />

              {/* Attempts Timeline */}
              <div>
                <h3 className="text-sm font-semibold mb-4">
                  {t('communication.deliveryLog.detail.attempts')}
                </h3>
                {log.attempts.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    {t('communication.deliveryLog.detail.noAttempts')}
                  </p>
                ) : (
                  <div className="space-y-4">
                    {log.attempts.map((attempt) => {
                      const Icon = STATUS_ICONS[attempt.status];
                      return (
                        <div key={attempt.id} className="flex gap-3 rounded-lg border p-3">
                          <Icon
                            className={`h-5 w-5 mt-0.5 shrink-0 ${
                              attempt.status === 'Delivered'
                                ? 'text-[var(--color-emerald-500)]'
                                : attempt.status === 'Failed' || attempt.status === 'Bounced'
                                ? 'text-destructive'
                                : 'text-muted-foreground'
                            }`}
                          />
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between gap-2">
                              <span className="text-sm font-medium">
                                {t('communication.deliveryLog.detail.attemptNumber', { number: attempt.attemptNumber })}
                              </span>
                              <span className="text-xs text-muted-foreground">
                                {format(new Date(attempt.attemptedAt), 'MMM d, HH:mm:ss')}
                              </span>
                            </div>
                            <div className="flex items-center gap-2 mt-1">
                              <Badge variant={STATUS_BADGE_VARIANT[attempt.status]} className="text-xs">
                                {t(`communication.deliveryLog.statusLabels.${attempt.status}`)}
                              </Badge>
                              {attempt.provider && (
                                <span className="text-xs text-muted-foreground">{attempt.provider}</span>
                              )}
                              {attempt.durationMs !== null && (
                                <span className="text-xs text-muted-foreground">
                                  {attempt.durationMs < 1000
                                    ? `${attempt.durationMs}ms`
                                    : `${(attempt.durationMs / 1000).toFixed(1)}s`}
                                </span>
                              )}
                            </div>
                            {attempt.errorMessage && (
                              <p className="text-xs text-destructive mt-1">{attempt.errorMessage}</p>
                            )}
                            {attempt.providerResponse && (
                              <details className="mt-1">
                                <summary className="text-xs text-muted-foreground cursor-pointer">
                                  {t('communication.deliveryLog.detail.providerResponse')}
                                </summary>
                                <pre className="text-xs text-muted-foreground bg-muted rounded p-2 mt-1 overflow-x-auto max-h-24">
                                  {attempt.providerResponse}
                                </pre>
                              </details>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          )}
        </SheetBody>
      </SheetContent>
    </Sheet>
  );
}
