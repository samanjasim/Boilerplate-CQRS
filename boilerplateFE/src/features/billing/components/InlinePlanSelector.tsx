import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ConfirmDialog } from '@/components/common';
import { usePlans, useChangeTenantPlan } from '../api';
import type { SubscriptionPlan } from '@/types';

interface InlinePlanSelectorProps {
  currentPlanId: string;
  tenantId: string;
  tenantName: string;
}

export function InlinePlanSelector({ currentPlanId, tenantId, tenantName }: InlinePlanSelectorProps) {
  const { t } = useTranslation();
  const [pendingPlanId, setPendingPlanId] = useState<string | null>(null);

  const { data: plansData } = usePlans({ pageSize: 50 });
  const { mutate: changePlan, isPending } = useChangeTenantPlan();

  const plans: SubscriptionPlan[] = (plansData as { data?: SubscriptionPlan[] })?.data ??
    (Array.isArray(plansData) ? (plansData as SubscriptionPlan[]) : []);

  const pendingPlanName = plans.find((p) => p.id === pendingPlanId)?.name ?? '';

  const handleValueChange = (newPlanId: string) => {
    if (newPlanId !== currentPlanId) {
      setPendingPlanId(newPlanId);
    }
  };

  const handleConfirm = () => {
    if (pendingPlanId) {
      changePlan(
        { tenantId, data: { planId: pendingPlanId } },
        { onSuccess: () => setPendingPlanId(null) },
      );
    }
  };

  return (
    <>
      <div onClick={(e) => e.stopPropagation()}>
        <Select value={currentPlanId} onValueChange={handleValueChange}>
          <SelectTrigger className="h-8 w-[140px] text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {plans
              .filter((p) => p.isActive)
              .map((plan) => (
                <SelectItem key={plan.id} value={plan.id}>
                  {plan.name}
                </SelectItem>
              ))}
          </SelectContent>
        </Select>
      </div>

      <div onClick={(e) => e.stopPropagation()}>
        <ConfirmDialog
          isOpen={!!pendingPlanId}
          onClose={() => setPendingPlanId(null)}
          onConfirm={handleConfirm}
          title={t('billing.changePlan')}
          description={t('billing.changePlanConfirm', { tenantName, planName: pendingPlanName })}
          confirmLabel={t('billing.changePlan')}
          variant="primary"
          isLoading={isPending}
        />
      </div>
    </>
  );
}
