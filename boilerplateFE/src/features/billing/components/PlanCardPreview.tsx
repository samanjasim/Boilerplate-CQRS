import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { PricingIntervalToggle } from './PricingIntervalToggle';
import { parseTranslations } from '../utils/plan-translations';
import type { PlanFeatureEntry } from '@/types';

type BillingInterval = 'Monthly' | 'Annual';
type PreviewLocale = 'en' | 'ar' | 'ku';

const PREVIEW_LOCALES: { code: PreviewLocale; dir: 'ltr' | 'rtl' }[] = [
  { code: 'en', dir: 'ltr' },
  { code: 'ar', dir: 'rtl' },
  { code: 'ku', dir: 'rtl' },
];

export interface PlanCardPreviewProps {
  name: string;
  description: string;
  /** Raw JSON string of plan translations — same shape stored on the entity. */
  translations: string;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  features: PlanFeatureEntry[];
  isFree: boolean;
  trialDays: number;
}

function getFeatureLabels(features: PlanFeatureEntry[], locale: PreviewLocale): string[] {
  if (!Array.isArray(features) || features.length === 0) return [];
  return features
    .filter((f) => f.value !== 'false')
    .map((f) => {
      const localized = f.translations?.[locale]?.label;
      const fallback = f.translations?.en?.label;
      if (localized) return localized;
      if (fallback) return fallback;
      return f.key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()).trim();
    });
}

/**
 * Live preview of how the plan renders on the public PricingPage. Mirrors
 * the visual treatment in `PricingPage`'s `PricingCard` (gradient-text price,
 * copper feature checkmarks, trial outline pill). The preview includes both a
 * billing interval toggle (Monthly / Annual) and a locale toggle (EN / AR /
 * KU) so admins can verify both pricing and translation overrides without
 * leaving the dialog.
 */
export function PlanCardPreview({
  name,
  description,
  translations,
  monthlyPrice,
  annualPrice,
  currency,
  features,
  isFree,
  trialDays,
}: PlanCardPreviewProps) {
  const { t } = useTranslation();
  const [interval, setInterval] = useState<BillingInterval>('Monthly');
  const [locale, setLocale] = useState<PreviewLocale>('en');

  const parsedTranslations = useMemo(() => parseTranslations(translations) ?? {}, [translations]);
  const localeOverrides = parsedTranslations[locale] ?? {};
  const dir = PREVIEW_LOCALES.find((l) => l.code === locale)?.dir ?? 'ltr';

  const displayName = (localeOverrides.name?.trim() || name.trim() ||
    t('billing.planForm.previewPlaceholderName'));
  const displayDescription = localeOverrides.description?.trim() || description.trim();

  const price = interval === 'Monthly' ? monthlyPrice : annualPrice;
  const featureLabels = getFeatureLabels(features, locale);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <div className="text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground/70">
          {t('billing.planForm.preview')}
        </div>
      </div>

      {/* Toggles — both prominent above the card so admins notice them. */}
      <div className="flex flex-col items-stretch gap-2.5">
        <div className="flex justify-center">
          <PricingIntervalToggle value={interval} onChange={setInterval} />
        </div>
        <div className="flex justify-center">
          <LocaleToggle value={locale} onChange={setLocale} />
        </div>
      </div>

      <Card variant="glass" className="h-full" dir={dir}>
        <CardContent className={cn('flex h-full flex-col gap-4 p-5')}>
          <div>
            <h3 className="text-base font-bold text-foreground break-words">{displayName}</h3>
            {displayDescription && (
              <p className="mt-1 text-xs text-muted-foreground line-clamp-2">{displayDescription}</p>
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

      {/* Hint when previewing a non-default locale that has no override yet —
       *  helps admins notice what they're configuring vs what falls back. */}
      {locale !== 'en' && !localeOverrides.name?.trim() && (
        <p className="text-[10px] text-muted-foreground/70 text-center">
          {t('billing.planForm.previewLocaleFallback', { locale: locale.toUpperCase() })}
        </p>
      )}
    </div>
  );
}

interface LocaleToggleProps {
  value: PreviewLocale;
  onChange: (next: PreviewLocale) => void;
}

function LocaleToggle({ value, onChange }: LocaleToggleProps) {
  return (
    <div className="inline-flex items-center gap-1 rounded-[12px] border border-border/40 bg-foreground/5 p-1">
      {PREVIEW_LOCALES.map((l) => (
        <button
          key={l.code}
          type="button"
          onClick={() => onChange(l.code)}
          aria-pressed={value === l.code}
          className={cn(
            'h-7 px-3 rounded-[8px] text-[11px] font-mono font-semibold uppercase tracking-wide motion-safe:transition-colors motion-safe:duration-150',
            value === l.code ? 'pill-active' : 'state-hover',
          )}
        >
          {l.code}
        </button>
      ))}
    </div>
  );
}
