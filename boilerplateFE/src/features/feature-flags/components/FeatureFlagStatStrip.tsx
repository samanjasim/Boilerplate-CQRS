import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

interface FeatureFlagStatStripProps {
  enabledCount: number;
  totalCount: number;
  tenantOverrideCount: number;
  optedOutCount: number;
  className?: string;
}

interface StatCardProps {
  label: string;
  value: number;
  secondary?: string;
  emphasis?: boolean;
}

function StatCard({ label, value, secondary, emphasis }: StatCardProps) {
  return (
    <Card variant="elevated">
      <CardContent className="pt-5">
        <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
        <div className="mt-2 flex items-baseline gap-2">
          <span
            className={cn(
              'text-2xl font-semibold tabular-nums',
              emphasis && 'gradient-text'
            )}
          >
            {value}
          </span>
          {secondary && <span className="text-sm text-muted-foreground">{secondary}</span>}
        </div>
      </CardContent>
    </Card>
  );
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
      <StatCard
        label={t('featureFlags.stats.enabledFlags')}
        value={enabledCount}
        secondary={`/ ${totalCount}`}
        emphasis
      />
      <StatCard
        label={t('featureFlags.stats.tenantOverrides')}
        value={tenantOverrideCount}
      />
      <StatCard
        label={t('featureFlags.stats.optedOut')}
        value={optedOutCount}
        emphasis
      />
    </div>
  );
}
