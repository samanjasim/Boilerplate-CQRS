import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import { useSubscriptionStatusCounts } from '../api/billing.queries';

export function SubscriptionStatusHero() {
  const { t } = useTranslation();
  const { data, isLoading } = useSubscriptionStatusCounts();

  if (isLoading || !data) {
    return (
      <div className="mb-6 grid gap-4 sm:grid-cols-2">
        <MetricCard
          label={t('billing.statusHero.active')}
          value="-"
          tone="active"
          eyebrow={t('billing.statusHero.activeEyebrow')}
        />
        <MetricCard
          label={t('billing.statusHero.trialing')}
          value="-"
          eyebrow={t('billing.statusHero.trialingEyebrow')}
        />
      </div>
    );
  }

  const showPastDue = data.pastDue > 0;

  return (
    <div className={cn('mb-6 grid gap-4 sm:grid-cols-2', showPastDue && 'lg:grid-cols-3')}>
      <MetricCard
        label={t('billing.statusHero.active')}
        eyebrow={t('billing.statusHero.activeEyebrow')}
        value={data.active}
        emphasis={data.active > 0}
        tone="active"
      />
      <MetricCard
        label={t('billing.statusHero.trialing')}
        eyebrow={t('billing.statusHero.trialingEyebrow')}
        value={data.trialing}
      />
      {showPastDue && (
        <MetricCard
          label={t('billing.statusHero.pastDue')}
          eyebrow={t('billing.statusHero.pastDueEyebrow')}
          value={data.pastDue}
          tone="destructive"
        />
      )}
    </div>
  );
}
