import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

type BillingInterval = 'Monthly' | 'Annual';

export interface PricingIntervalToggleProps {
  value: BillingInterval;
  onChange: (next: BillingInterval) => void;
}

export function PricingIntervalToggle({ value, onChange }: PricingIntervalToggleProps) {
  const { t } = useTranslation();

  const segment = (key: BillingInterval, label: string, badge?: string) => (
    <button
      type="button"
      onClick={() => onChange(key)}
      aria-pressed={value === key}
      className={cn(
        'inline-flex h-9 items-center gap-2 rounded-[10px] px-4 text-sm motion-safe:transition-colors motion-safe:duration-150',
        value === key ? 'pill-active' : 'state-hover'
      )}
    >
      {label}
      {badge && (
        <span className="rounded-full bg-primary/15 px-1.5 py-0 font-mono text-[10px] text-primary">
          {badge}
        </span>
      )}
    </button>
  );

  return (
    <div className="inline-flex items-center gap-1 rounded-[12px] border border-border/40 bg-foreground/5 p-1">
      {segment('Monthly', t('billing.pricing.toggleMonthly'))}
      {segment('Annual', t('billing.pricing.toggleAnnual'), t('billing.pricing.saveBadge'))}
    </div>
  );
}
