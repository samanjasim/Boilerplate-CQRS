import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { cn } from '@/lib/utils';
import { usePlans, useChangePlan } from '../api';
import { getFeatureLabels } from '../utils/features';
import type { TenantSubscription } from '@/types';

interface PlanSelectorModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  subscription: TenantSubscription | undefined;
}

export function PlanSelectorModal({ open, onOpenChange, subscription }: PlanSelectorModalProps) {
  const { t } = useTranslation();
  const [interval, setInterval] = useState<'Monthly' | 'Annual'>(
    subscription?.billingInterval ?? 'Monthly'
  );

  const { data: plans, isLoading } = usePlans({ pageSize: 50 });
  const { mutate: changePlan, isPending } = useChangePlan();

  const planList = (plans as { data?: unknown[] })?.data ?? (Array.isArray(plans) ? plans : []);

  const annualSavingsPct = 20; // generic hint — plans show actual prices

  const handleSelect = (planId: string) => {
    changePlan(
      { planId, interval: interval === 'Annual' ? 1 : 0 },
      { onSuccess: () => onOpenChange(false) }
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('billing.changePlan')}</DialogTitle>
          <DialogDescription>{t('billing.pricingSubtitle')}</DialogDescription>
        </DialogHeader>

        {/* Interval toggle */}
        <div className="flex items-center justify-center gap-2 my-2">
          <button
            type="button"
            onClick={() => setInterval('Monthly')}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              interval === 'Monthly'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            )}
          >
            {t('billing.monthly')}
          </button>
          <button
            type="button"
            onClick={() => setInterval('Annual')}
            className={cn(
              'flex items-center gap-1.5 rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              interval === 'Annual'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            )}
          >
            {t('billing.annual')}
            <Badge variant="secondary" className="text-xs">
              {t('billing.savePercent', { percent: annualSavingsPct })}
            </Badge>
          </button>
        </div>

        {isLoading ? (
          <div className="flex justify-center py-12">
            <Spinner size="lg" />
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {(planList as import('@/types').SubscriptionPlan[]).map((plan) => {
              const isCurrent = subscription?.subscriptionPlanId === plan.id;
              const price = interval === 'Monthly' ? plan.monthlyPrice : plan.annualPrice;
              const features = getFeatureLabels(plan.features);

              return (
                <div
                  key={plan.id}
                  className={cn(
                    'rounded-2xl border p-5 flex flex-col gap-3 transition-shadow',
                    isCurrent
                      ? 'border-primary bg-primary/5'
                      : 'border-border bg-card hover:shadow-card'
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <h3 className="font-semibold text-foreground">{plan.name}</h3>
                    {isCurrent && (
                      <Badge variant="default" className="shrink-0 text-xs">
                        {t('billing.currentLabel')}
                      </Badge>
                    )}
                    {plan.isFree && !isCurrent && (
                      <Badge variant="secondary" className="shrink-0 text-xs">
                        {t('billing.freeLabel')}
                      </Badge>
                    )}
                  </div>

                  <div className="text-2xl font-bold text-foreground">
                    {plan.isFree ? (
                      t('billing.freeLabel')
                    ) : (
                      <>
                        {plan.currency} {price.toFixed(2)}
                        <span className="text-sm font-normal text-muted-foreground">
                          {interval === 'Monthly' ? t('billing.perMonth') : t('billing.perYear')}
                        </span>
                      </>
                    )}
                  </div>

                  {plan.description && (
                    <p className="text-sm text-muted-foreground">{plan.description}</p>
                  )}

                  {features.length > 0 && (
                    <ul className="space-y-1 flex-1">
                      {features.slice(0, 5).map((f, i) => (
                        <li key={i} className="flex items-center gap-2 text-sm text-foreground">
                          <Check className="h-3.5 w-3.5 shrink-0 text-primary" />
                          {f}
                        </li>
                      ))}
                    </ul>
                  )}

                  <Button
                    className="w-full mt-auto"
                    variant={isCurrent ? 'outline' : 'default'}
                    disabled={isCurrent || isPending}
                    onClick={() => !isCurrent && handleSelect(plan.id)}
                  >
                    {isCurrent ? t('billing.currentLabel') : t('billing.changePlan')}
                  </Button>
                </div>
              );
            })}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
