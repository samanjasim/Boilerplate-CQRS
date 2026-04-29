import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';

interface FeatureFlagStatStripProps {
  enabledCount: number;
  totalCount: number;
  tenantOverrideCount: number;
  optedOutCount: number;
  className?: string;
}

export function FeatureFlagStatStrip({
  enabledCount,
  totalCount,
  tenantOverrideCount,
  optedOutCount,
  className,
}: FeatureFlagStatStripProps) {
  const { t } = useTranslation();

  return (
    <div className={cn('grid gap-4 sm:grid-cols-3', className)}>
      <MetricCard
        label={t('featureFlags.stats.enabledFlags')}
        value={enabledCount}
        secondary={`/ ${totalCount}`}
        emphasis
      />
      <MetricCard
        label={t('featureFlags.stats.tenantOverrides')}
        value={tenantOverrideCount}
      />
      <MetricCard
        label={t('featureFlags.stats.optedOut')}
        value={optedOutCount}
        emphasis
      />
    </div>
  );
}
