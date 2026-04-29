import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Check, Blocks } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { cn } from '@/lib/utils';
import { usePlans } from '../api';
import { PricingIntervalToggle } from '../components/PricingIntervalToggle';
import { pickPopularPlan } from '../utils/popular-plan';
import { useAuthStore, selectUser } from '@/stores';
import { ROUTES } from '@/config';
import type { SubscriptionPlan, PlanFeatureEntry } from '@/types';

type BillingInterval = 'Monthly' | 'Annual';

function getFeatureLabels(features: PlanFeatureEntry[]): string[] {
  if (!Array.isArray(features) || features.length === 0) return [];
  return features
    .filter((f) => !(f.value === 'false'))
    .map((f) => {
      const label = f.translations?.en?.label;
      if (label) return label;
      return f.key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()).trim();
    });
}

export default function PricingPage() {
  const { t } = useTranslation();
  const appName = import.meta.env.VITE_APP_NAME || 'Starter';
  const user = useAuthStore(selectUser);
  const [interval, setInterval] = useState<BillingInterval>('Monthly');

  const { data: plansData, isLoading } = usePlans({ pageSize: 50 });
  const plans: SubscriptionPlan[] = useMemo(
    () =>
      (plansData as { data?: SubscriptionPlan[] })?.data ??
      (Array.isArray(plansData) ? (plansData as SubscriptionPlan[]) : []),
    [plansData]
  );
  const popularPlan = useMemo(() => pickPopularPlan(plans), [plans]);

  return (
    <div className="relative min-h-screen gradient-hero overflow-hidden">
      <div className="relative z-10 mx-auto max-w-6xl px-6 py-16">
        {/* Nav bar */}
        <div className="flex items-center justify-between mb-14">
          <div className="flex items-center gap-2">
            <div className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-white/10">
              <Blocks className="h-4 w-4 text-white" />
            </div>
            <span className="font-bold text-white text-lg">{appName}</span>
          </div>
          <div className="flex items-center gap-3">
            {user ? (
              <Button asChild variant="outline" size="sm" className="border-white/30 bg-white/10 text-white hover:bg-white/15">
                <Link to={ROUTES.DASHBOARD}>{t('landing.signIn')}</Link>
              </Button>
            ) : (
              <>
                <Button asChild variant="outline" size="sm" className="border-white/30 bg-white/10 text-white hover:bg-white/15">
                  <Link to={ROUTES.LOGIN}>{t('landing.signIn')}</Link>
                </Button>
                <Button asChild size="sm">
                  <Link to={ROUTES.REGISTER_TENANT}>{t('billing.pricing.getStarted')}</Link>
                </Button>
              </>
            )}
          </div>
        </div>

        {/* Heading */}
        <div className="text-center mb-10">
          <h1 className="text-4xl font-bold text-white mb-3">{t('billing.pricingTitle')}</h1>
          <p className="text-lg text-white/80 max-w-md mx-auto">{t('billing.pricingSubtitle')}</p>

          {/* Interval toggle */}
          <div className="mt-6">
            <PricingIntervalToggle value={interval} onChange={setInterval} />
          </div>
        </div>

        {/* Plan cards */}
        {isLoading ? (
          <div className="flex justify-center py-16">
            <Spinner size="lg" />
          </div>
        ) : (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
            {plans.map((plan) => {
              const price = interval === 'Monthly' ? plan.monthlyPrice : plan.annualPrice;
              const features = getFeatureLabels(plan.features);

              return (
                <PricingCard
                  key={plan.id}
                  plan={plan}
                  price={price}
                  interval={interval}
                  features={features}
                  isCurrentPlan={false} // public page — no subscription context
                  isLoggedIn={!!user}
                  isPopular={plan.id === popularPlan?.id}
                />
              );
            })}
          </div>
        )}

        {/* Footer note */}
        <p className="text-center text-white/50 text-sm mt-12">
          {t('billing.pricingSubtitle')}
        </p>
      </div>
    </div>
  );
}

interface PricingCardProps {
  plan: SubscriptionPlan;
  price: number;
  interval: BillingInterval;
  features: string[];
  isCurrentPlan: boolean;
  isLoggedIn: boolean;
  isPopular: boolean;
}

function PricingCard({
  plan,
  price,
  interval,
  features,
  isCurrentPlan,
  isLoggedIn,
  isPopular,
}: PricingCardProps) {
  const { t } = useTranslation();
  const ctaLabel = isLoggedIn ? t('billing.pricing.upgrade') : t('billing.pricing.getStarted');
  const ctaTarget = isLoggedIn ? ROUTES.BILLING : ROUTES.REGISTER_TENANT;

  return (
    <div
      className={cn(
        'relative motion-safe:transition-transform motion-safe:duration-200',
        isPopular && 'lg:scale-105'
      )}
    >
      {isPopular && (
        <div className="absolute -top-3 left-1/2 z-10 -translate-x-1/2">
          <span className="rounded-full bg-primary px-3 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-primary-foreground">
            {t('billing.pricing.popular')}
          </span>
        </div>
      )}
      <Card variant="glass" className={cn('h-full', isPopular && 'glow-primary-md border border-primary/30')}>
        <CardContent className="flex h-full flex-col gap-4 p-6">
          <div>
            <h3 className="text-lg font-bold text-foreground">{plan.name}</h3>
            {plan.description && (
              <p className="mt-1 text-sm text-muted-foreground">{plan.description}</p>
            )}
          </div>

          <div>
            {plan.isFree ? (
              <span className="text-3xl font-bold tabular-nums gradient-text">
                {t('billing.freeLabel')}
              </span>
            ) : (
              <div className="flex items-baseline gap-1">
                <span className="text-3xl font-bold tabular-nums gradient-text">
                  {plan.currency} {price.toFixed(2)}
                </span>
                <span className="text-sm text-muted-foreground">
                  {interval === 'Monthly' ? t('billing.perMonth') : t('billing.perYear')}
                </span>
              </div>
            )}
            {plan.trialDays > 0 && (
              <span className="mt-2 inline-flex rounded-full border border-primary/40 bg-primary/5 px-2 py-0.5 text-xs text-primary">
                {t('billing.pricing.trialDays', { count: plan.trialDays })}
              </span>
            )}
          </div>

          {features.length > 0 && (
            <ul className="flex-1 space-y-2">
              {features.map((feature) => (
                <li key={feature} className="flex items-start gap-2 text-sm">
                  <Check className="mt-0.5 h-4 w-4 shrink-0 text-primary" />
                  <span className="text-muted-foreground">{feature}</span>
                </li>
              ))}
            </ul>
          )}

          <div className="mt-auto">
            {isCurrentPlan ? (
              <Button className="w-full" variant="ghost" disabled>
                {t('billing.pricing.currentPlan')}
              </Button>
            ) : (
              <Button asChild className="w-full" variant={plan.isFree ? 'outline' : 'default'}>
                <Link to={ctaTarget}>{ctaLabel}</Link>
              </Button>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
