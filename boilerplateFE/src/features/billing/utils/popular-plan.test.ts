import { describe, expect, it } from 'vitest';
import { pickPopularPlan } from './popular-plan';
import type { SubscriptionPlan } from '@/types';

const make = (overrides: Partial<SubscriptionPlan>): SubscriptionPlan => ({
  id: overrides.id ?? 'plan',
  name: overrides.name ?? 'Plan',
  slug: overrides.slug ?? 'plan',
  description: '',
  translations: null,
  monthlyPrice: 10,
  annualPrice: 100,
  currency: 'USD',
  features: [],
  isFree: false,
  isActive: true,
  isPublic: true,
  displayOrder: 0,
  trialDays: 0,
  subscriberCount: 0,
  createdAt: '2026-04-29',
  modifiedAt: '2026-04-29',
  ...overrides,
});

describe('pickPopularPlan', () => {
  it('returns the plan with the highest subscriber count among public non-free active plans', () => {
    const plans = [
      make({ id: 'a', subscriberCount: 5 }),
      make({ id: 'b', subscriberCount: 50 }),
      make({ id: 'c', subscriberCount: 10 }),
    ];

    expect(pickPopularPlan(plans)?.id).toBe('b');
  });

  it('breaks ties by lowest display order', () => {
    const plans = [
      make({ id: 'a', subscriberCount: 10, displayOrder: 2 }),
      make({ id: 'b', subscriberCount: 10, displayOrder: 1 }),
      make({ id: 'c', subscriberCount: 10, displayOrder: 3 }),
    ];

    expect(pickPopularPlan(plans)?.id).toBe('b');
  });

  it('skips free plans', () => {
    const plans = [
      make({ id: 'free', subscriberCount: 100, isFree: true }),
      make({ id: 'paid', subscriberCount: 5 }),
    ];

    expect(pickPopularPlan(plans)?.id).toBe('paid');
  });

  it('skips inactive plans', () => {
    const plans = [
      make({ id: 'old', subscriberCount: 100, isActive: false }),
      make({ id: 'live', subscriberCount: 5 }),
    ];

    expect(pickPopularPlan(plans)?.id).toBe('live');
  });

  it('skips non-public plans', () => {
    const plans = [
      make({ id: 'private', subscriberCount: 100, isPublic: false }),
      make({ id: 'public', subscriberCount: 5 }),
    ];

    expect(pickPopularPlan(plans)?.id).toBe('public');
  });

  it('returns null when no eligible plans exist', () => {
    expect(pickPopularPlan([make({ isFree: true })])).toBeNull();
    expect(pickPopularPlan([])).toBeNull();
  });
});
