import type { SubscriptionPlan } from '@/types';

export function pickPopularPlan(plans: SubscriptionPlan[]): SubscriptionPlan | null {
  const candidates = plans.filter((plan) => plan.isPublic && plan.isActive && !plan.isFree);

  const first = candidates[0];
  if (!first) return null;

  return candidates.reduce<SubscriptionPlan>((best, plan) => {
    if (plan.subscriberCount > best.subscriberCount) return plan;
    if (plan.subscriberCount === best.subscriberCount && plan.displayOrder < best.displayOrder) {
      return plan;
    }
    return best;
  }, first);
}
