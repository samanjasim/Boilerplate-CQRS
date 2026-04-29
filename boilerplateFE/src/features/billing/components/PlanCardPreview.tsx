import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { PricingIntervalToggle } from './PricingIntervalToggle';
import type { PlanFeatureEntry } from '@/types';

type BillingInterval = 'Monthly' | 'Annual';

export interface PlanCardPreviewProps {
  name: string;
  description: string;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  features: PlanFeatureEntry[];
  isFree: boolean;
  trialDays: number;
}

function getFeatureLabels(features: PlanFeatureEntry[]): string[] {
  if (!Array.isArray(features) || features.length === 0) return [];
  return features
    .filter((f) => f.value !== 'false')
    .map((f) => {
      const label = f.translations?.en?.label;
      if (label) return label;
      return f.key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()).trim();
    });
}

/**
 * Live preview of how the plan renders on the public PricingPage. Mirrors
 * the visual treatment in `PricingPage`'s `PricingCard` (gradient-text price,
 * copper feature checkmarks, trial outline pill) without the CTA / current-
 * plan plumbing — this is purely visual feedback for an admin filling the
 * create/edit form.
 */
export function PlanCardPreview({
  name,
  description,
  monthlyPrice,
  annualPrice,
  currency,
  features,
  isFree,
  trialDays,
}: PlanCardPreviewProps) {
  const { t } = useTranslation();
  const [interval, setInterval] = useState<BillingInterval>('Monthly');

  const price = interval === 'Monthly' ? monthlyPrice : annualPrice;
  const featureLabels = getFeatureLabels(features);
  const displayName = name.trim() || t('billing.planForm.previewPlaceholderName');

  return (
    <div className="space-y-3">
      <div className="text-[10px] uppercase tracking-[0.12em] text-muted-foreground/70">
        {t('billing.planForm.preview')}
      </div>

      <Card variant="glass" className="h-full">
        <CardContent className={cn('flex h-full flex-col gap-4 p-5')}>
          <div>
            <h3 className="text-base font-bold text-foreground break-words">{displayName}</h3>
            {description && (
              <p className="mt-1 text-xs text-muted-foreground line-clamp-2">{description}</p>
            )}
          </div>

          <div>
            {isFree ? (
              <span className="text-2xl font-bold tabular-nums gradient-text">
                {t('billing.freeLabel')}
              </span>
            ) : (
              <div className="flex items-baseline gap-1 flex-wrap">
                <span className="text-2xl font-bold tabular-nums gradient-text">
                  {currency} {price.toFixed(2)}
                </span>
                <span className="text-xs text-muted-foreground">
                  {interval === 'Monthly' ? t('billing.perMonth') : t('billing.perYear')}
                </span>
              </div>
            )}
            {trialDays > 0 && (
              <span className="mt-2 inline-flex rounded-full border border-primary/40 bg-primary/5 px-2 py-0.5 text-[10px] text-primary">
                {t('billing.pricing.trialDays', { count: trialDays })}
              </span>
            )}
          </div>

          {featureLabels.length > 0 ? (
            <ul className="flex-1 space-y-1.5">
              {featureLabels.slice(0, 6).map((feature) => (
                <li key={feature} className="flex items-start gap-2 text-xs">
                  <Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-primary" />
                  <span className="text-muted-foreground line-clamp-1">{feature}</span>
                </li>
              ))}
              {featureLabels.length > 6 && (
                <li className="text-[10px] text-muted-foreground/70 ps-5">
                  {t('billing.planForm.previewMoreFeatures', { count: featureLabels.length - 6 })}
                </li>
              )}
            </ul>
          ) : (
            <p className="text-xs text-muted-foreground/60 italic">
              {t('billing.planForm.previewNoFeatures')}
            </p>
          )}
        </CardContent>
      </Card>

      <div className="flex justify-center pt-1">
        <PricingIntervalToggle value={interval} onChange={setInterval} />
      </div>
    </div>
  );
}
