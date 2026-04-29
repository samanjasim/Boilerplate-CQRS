import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { MetricCard } from '@/components/common';
import { STATUS_BADGE_VARIANT } from '@/constants';
import { cn } from '@/lib/utils';
import { formatDate, formatFileSize } from '@/utils/format';
import type { ReactNode } from 'react';
import type { TenantSubscription, Usage } from '@/types';

export interface BillingHeroProps {
  subscription: TenantSubscription | undefined;
  usage: Usage | undefined;
  eyebrow?: string;
  action?: ReactNode;
  isLoading?: boolean;
}

interface UsageRow {
  label: string;
  current: number;
  max: number;
  format?: (value: number) => string;
}

function progressClass(percent: number): string {
  if (percent >= 90) return 'bg-destructive';
  if (percent >= 75) return 'bg-warning';
  return 'bg-primary';
}

function ProgressMetric({ label, current, max, format }: UsageRow) {
  const percent = max > 0 ? Math.min(100, Math.round((current / max) * 100)) : 0;
  const formatValue = format ?? ((value: number) => String(value));

  return (
    <MetricCard
      label={label}
      value={formatValue(current)}
      secondary={`/ ${formatValue(max)}`}
      eyebrow={`${percent}%`}
    >
      <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-muted">
        <div
          className={cn('h-full rounded-full transition-all', progressClass(percent))}
          style={{ width: `${percent}%` }}
        />
      </div>
    </MetricCard>
  );
}

export function BillingHero({ subscription, usage, eyebrow, action, isLoading }: BillingHeroProps) {
  const { t } = useTranslation();

  if (isLoading || !subscription) {
    return (
      <div className="mb-6 grid gap-4 lg:grid-cols-12">
        <Card variant="glass" className="lg:col-span-7">
          <CardContent className="min-h-[120px] py-5" />
        </Card>
        <div className="grid gap-3 sm:grid-cols-3 lg:col-span-5">
          {Array.from({ length: 3 }).map((_, index) => (
            <Card key={index} variant="glass">
              <CardContent className="min-h-[88px] py-5" />
            </Card>
          ))}
        </div>
      </div>
    );
  }

  const isMonthly = subscription.billingInterval === 'Monthly';
  const isFree = subscription.lockedMonthlyPrice === 0 && subscription.lockedAnnualPrice === 0;
  const price = isFree
    ? t('billing.hero.free')
    : `${isMonthly ? subscription.lockedMonthlyPrice : subscription.lockedAnnualPrice} ${
        subscription.currency
      } ${isMonthly ? t('billing.hero.perMonth') : t('billing.hero.perYear')}`;

  const rows: UsageRow[] | null = usage
    ? [
        { label: t('billing.hero.usageUsers'), current: usage.users, max: usage.maxUsers },
        {
          label: t('billing.hero.usageStorage'),
          current: usage.storageBytes,
          max: usage.maxStorageBytes,
          format: formatFileSize,
        },
        { label: t('billing.hero.usageWebhooks'), current: usage.webhooks, max: usage.maxWebhooks },
      ]
    : null;

  return (
    <div className="mb-6 grid gap-4 lg:grid-cols-12">
      <Card variant="glass" className="lg:col-span-7">
        <CardContent className="flex flex-wrap items-start gap-4 py-5">
          <div className="min-w-0 flex-1">
            {eyebrow && (
              <div className="mb-1 text-[10px] uppercase tracking-[0.12em] text-muted-foreground">
                {eyebrow}
              </div>
            )}
            <div className="text-xs uppercase tracking-wide text-muted-foreground">
              {t('billing.hero.yourPlan')}
            </div>
            <div className="mt-1 flex flex-wrap items-baseline gap-3">
              <span className="text-2xl font-semibold gradient-text">{subscription.planName}</span>
              <Badge variant={STATUS_BADGE_VARIANT[subscription.status] ?? 'outline'}>
                {subscription.status}
              </Badge>
              <span className="text-sm text-muted-foreground tabular-nums">{price}</span>
            </div>
            <div className="mt-1 text-xs text-muted-foreground">
              {t('billing.hero.period', {
                start: formatDate(subscription.currentPeriodStart),
                end: formatDate(subscription.currentPeriodEnd),
              })}
            </div>
            {!subscription.autoRenew && (
              <div className="mt-1 text-[10px] uppercase tracking-[0.12em] text-warning">
                {t('billing.hero.autoRenewOff')}
              </div>
            )}
          </div>
          {action && <div className="shrink-0">{action}</div>}
        </CardContent>
      </Card>

      <div className="grid gap-3 sm:grid-cols-3 lg:col-span-5">
        {rows?.map((row) => <ProgressMetric key={row.label} {...row} />) ??
          Array.from({ length: 3 }).map((_, index) => (
            <Card key={index} variant="glass">
              <CardContent className="min-h-[88px] py-5" />
            </Card>
          ))}
      </div>
    </div>
  );
}
