import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import { useDeliveryStatusCounts } from '../api/communication.queries';

export function DeliveryLogStatusHero() {
  const { t } = useTranslation();
  const { data, isLoading } = useDeliveryStatusCounts(7);

  if (isLoading || !data?.data) {
    return null;
  }

  const counts = data.data;
  const total = counts.delivered + counts.failed + counts.pending + counts.bounced;
  if (total === 0) {
    return null;
  }

  const showDelivered = counts.delivered > 0;
  const showFailed = counts.failed > 0;
  const showPending = counts.pending > 0;
  const showBounced = counts.bounced > 0;

  const visibleCount = [showDelivered, showFailed, showPending, showBounced].filter(Boolean).length;

  return (
    <div className="space-y-2 mb-6">
      <div
        className={cn(
          'grid gap-4',
          visibleCount === 1 && 'sm:grid-cols-1',
          visibleCount === 2 && 'sm:grid-cols-2',
          visibleCount === 3 && 'sm:grid-cols-2 lg:grid-cols-3',
          visibleCount === 4 && 'sm:grid-cols-2 lg:grid-cols-4',
        )}
      >
        {showDelivered && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.delivered')}
            eyebrow={t('communication.deliveryLog.statusCounts.deliveredEyebrow')}
            value={counts.delivered}
            tone="active"
            emphasis
          />
        )}
        {showFailed && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.failed')}
            eyebrow={t('communication.deliveryLog.statusCounts.failedEyebrow')}
            value={counts.failed}
            tone="destructive"
            emphasis={counts.failed > 0}
          />
        )}
        {showPending && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.pending')}
            eyebrow={t('communication.deliveryLog.statusCounts.pendingEyebrow')}
            value={counts.pending}
          />
        )}
        {showBounced && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.bounced')}
            eyebrow={t('communication.deliveryLog.statusCounts.bouncedEyebrow')}
            value={counts.bounced}
            tone="destructive"
            emphasis={counts.bounced > 0}
          />
        )}
      </div>
      <p className="text-xs text-muted-foreground">
        {t('communication.deliveryLog.window.last7Days')}
      </p>
    </div>
  );
}
