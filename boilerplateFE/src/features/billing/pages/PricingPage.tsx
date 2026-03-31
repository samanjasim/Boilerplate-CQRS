import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Check, Blocks } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { cn } from '@/lib/utils';
import { usePlans } from '../api';
import { useAuthStore, selectUser } from '@/stores';
import { ROUTES } from '@/config';
import type { SubscriptionPlan } from '@/types';

function parseFeatures(features: string): string[] {
  try {
    const parsed = JSON.parse(features) as unknown;
    if (Array.isArray(parsed)) return parsed as string[];
  } catch {
    // not JSON
  }
  return features ? [features] : [];
}

export default function PricingPage() {
  const { t } = useTranslation();
  const appName = import.meta.env.VITE_APP_NAME || 'Starter';
  const user = useAuthStore(selectUser);
  const [interval, setInterval] = useState<'Monthly' | 'Annual'>('Monthly');

  const { data: plansData, isLoading } = usePlans({ pageSize: 50 });
  const plans: SubscriptionPlan[] = (plansData as { data?: SubscriptionPlan[] })?.data ??
    (Array.isArray(plansData) ? (plansData as SubscriptionPlan[]) : []);

  const annualSavingsPct = 20;

  return (
    <div className="relative min-h-screen gradient-hero overflow-hidden">
      {/* Decorative blurs */}
      <div className="absolute -top-24 -left-24 h-96 w-96 rounded-full bg-white/10 blur-3xl pointer-events-none" />
      <div className="absolute -bottom-24 -right-24 h-96 w-96 rounded-full bg-white/5 blur-3xl pointer-events-none" />

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
                <Button asChild size="sm" className="bg-white text-foreground hover:bg-white/90">
                  <Link to={ROUTES.REGISTER_TENANT}>{t('billing.getStarted')}</Link>
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
          <div className="inline-flex items-center gap-2 mt-6 rounded-full bg-white/10 p-1">
            <button
              type="button"
              onClick={() => setInterval('Monthly')}
              className={cn(
                'rounded-full px-5 py-1.5 text-sm font-medium transition-colors',
                interval === 'Monthly'
                  ? 'bg-white text-foreground shadow'
                  : 'text-white/80 hover:text-white'
              )}
            >
              {t('billing.monthly')}
            </button>
            <button
              type="button"
              onClick={() => setInterval('Annual')}
              className={cn(
                'flex items-center gap-1.5 rounded-full px-5 py-1.5 text-sm font-medium transition-colors',
                interval === 'Annual'
                  ? 'bg-white text-foreground shadow'
                  : 'text-white/80 hover:text-white'
              )}
            >
              {t('billing.annual')}
              <span className="rounded-full bg-primary px-1.5 py-0.5 text-[10px] font-semibold text-primary-foreground">
                {t('billing.savePercent', { percent: annualSavingsPct })}
              </span>
            </button>
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
              const features = parseFeatures(plan.features);

              return (
                <PricingCard
                  key={plan.id}
                  plan={plan}
                  price={price}
                  interval={interval}
                  features={features}
                  isCurrentPlan={false} // public page — no subscription context
                  isLoggedIn={!!user}
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
  interval: 'Monthly' | 'Annual';
  features: string[];
  isCurrentPlan: boolean;
  isLoggedIn: boolean;
}

function PricingCard({ plan, price, interval, features, isCurrentPlan, isLoggedIn }: PricingCardProps) {
  const { t } = useTranslation();

  return (
    <div
      className={cn(
        'rounded-2xl p-6 flex flex-col gap-4 transition-all',
        isCurrentPlan
          ? 'bg-white text-foreground shadow-xl ring-2 ring-white'
          : 'bg-white/10 text-white hover:bg-white/15 backdrop-blur-sm'
      )}
    >
      <div>
        <h3 className={cn('font-bold text-lg', isCurrentPlan ? 'text-foreground' : 'text-white')}>
          {plan.name}
        </h3>
        {plan.description && (
          <p className={cn('text-sm mt-1', isCurrentPlan ? 'text-muted-foreground' : 'text-white/70')}>
            {plan.description}
          </p>
        )}
      </div>

      <div>
        {plan.isFree ? (
          <span className={cn('text-3xl font-bold', isCurrentPlan ? 'text-foreground' : 'text-white')}>
            {t('billing.freeLabel')}
          </span>
        ) : (
          <div className="flex items-baseline gap-1">
            <span className={cn('text-3xl font-bold', isCurrentPlan ? 'text-foreground' : 'text-white')}>
              {plan.currency} {price.toFixed(2)}
            </span>
            <span className={cn('text-sm', isCurrentPlan ? 'text-muted-foreground' : 'text-white/60')}>
              {interval === 'Monthly' ? t('billing.perMonth') : t('billing.perYear')}
            </span>
          </div>
        )}
        {plan.trialDays > 0 && (
          <Badge variant="secondary" className="mt-1 text-xs">
            {plan.trialDays}d trial
          </Badge>
        )}
      </div>

      {features.length > 0 && (
        <ul className="space-y-2 flex-1">
          {features.map((f, i) => (
            <li key={i} className="flex items-start gap-2 text-sm">
              <Check className={cn('h-4 w-4 mt-0.5 shrink-0', isCurrentPlan ? 'text-primary' : 'text-white/80')} />
              <span className={isCurrentPlan ? 'text-foreground' : 'text-white/90'}>{f}</span>
            </li>
          ))}
        </ul>
      )}

      <div className="mt-auto">
        {isCurrentPlan ? (
          <Button className="w-full" variant="outline" disabled>
            {t('billing.currentLabel')}
          </Button>
        ) : isLoggedIn ? (
          <Button asChild className={cn('w-full', isCurrentPlan ? '' : 'bg-white text-foreground hover:bg-white/90')}>
            <Link to={ROUTES.BILLING}>
              {t('billing.upgrade')}
            </Link>
          </Button>
        ) : (
          <Button asChild className="w-full bg-white text-foreground hover:bg-white/90">
            <Link to={ROUTES.REGISTER_TENANT}>
              {t('billing.getStarted')}
            </Link>
          </Button>
        )}
      </div>
    </div>
  );
}
