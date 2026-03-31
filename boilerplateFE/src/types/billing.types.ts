export interface PlanFeatureTranslation {
  label: string;
}

export interface PlanFeatureEntry {
  key: string;
  value: string;
  translations?: Record<string, PlanFeatureTranslation>;
}

export interface PlanOption {
  key: string;
  name: string;
  description: string | null;
  valueType: 'Boolean' | 'Integer' | 'String' | 'Json';
  defaultValue: string;
  category: string;
}

export interface SubscriptionPlan {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  translations: string | null;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  features: PlanFeatureEntry[];
  isFree: boolean;
  isActive: boolean;
  isPublic: boolean;
  displayOrder: number;
  trialDays: number;
  subscriberCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface TenantSubscription {
  id: string;
  tenantId: string;
  subscriptionPlanId: string;
  planName: string;
  planSlug: string;
  status: 'Trialing' | 'Active' | 'PastDue' | 'Canceled' | 'Expired';
  lockedMonthlyPrice: number;
  lockedAnnualPrice: number;
  currency: string;
  billingInterval: 'Monthly' | 'Annual';
  currentPeriodStart: string;
  currentPeriodEnd: string;
  canceledAt: string | null;
  autoRenew: boolean;
  createdAt: string;
}

export interface PaymentRecord {
  id: string;
  amount: number;
  currency: string;
  status: 'Pending' | 'Completed' | 'Failed' | 'Refunded';
  description: string | null;
  periodStart: string;
  periodEnd: string;
  createdAt: string;
}

export interface Usage {
  users: number;
  storageBytes: number;
  apiKeys: number;
  reportsActive: number;
  maxUsers: number;
  maxStorageBytes: number;
  maxApiKeys: number;
  maxReports: number;
}

export interface CreatePlanData {
  name: string;
  slug: string;
  description?: string;
  translations?: string;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  features: PlanFeatureEntry[];
  isFree: boolean;
  isPublic: boolean;
  displayOrder: number;
  trialDays: number;
}

export interface UpdatePlanData extends CreatePlanData {
  id: string;
  priceChangeReason?: string;
}

export interface ChangePlanData {
  planId: string;
  interval?: 'Monthly' | 'Annual';
}

export interface SubscriptionSummary {
  tenantId: string;
  tenantName: string;
  tenantSlug: string | null;
  subscriptionPlanId: string;
  planName: string;
  planSlug: string;
  status: 'Trialing' | 'Active' | 'PastDue' | 'Canceled' | 'Expired';
  billingInterval: 'Monthly' | 'Annual';
  currentPeriodStart: string;
  currentPeriodEnd: string;
  usersCount: number;
  maxUsers: number;
  storageUsedMb: number;
  maxStorageMb: number;
  latestPaymentStatus: 'Pending' | 'Completed' | 'Failed' | 'Refunded' | null;
  createdAt: string;
}
