import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  SubscriptionPlan,
  TenantSubscription,
  Usage,
  CreatePlanData,
  UpdatePlanData,
  ChangePlanData,
} from '@/types';

export const billingApi = {
  // Plans (tenant view — public active plans)
  getPlans: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.BILLING.PLANS, { params }).then(r => r.data),

  // Plans (platform admin — all plans)
  getAllPlans: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.BILLING.PLANS_MANAGE, { params }).then(r => r.data),

  getPlanById: (id: string) =>
    apiClient.get<{ data: SubscriptionPlan }>(API_ENDPOINTS.BILLING.PLAN_DETAIL(id)).then(r => r.data.data),

  createPlan: (data: CreatePlanData) =>
    apiClient.post(API_ENDPOINTS.BILLING.PLANS_CREATE, data).then(r => r.data),

  updatePlan: (data: UpdatePlanData) =>
    apiClient.put(API_ENDPOINTS.BILLING.PLAN_DETAIL(data.id), data).then(r => r.data),

  deactivatePlan: (id: string) =>
    apiClient.delete(API_ENDPOINTS.BILLING.PLAN_DETAIL(id)).then(r => r.data),

  resyncPlan: (id: string) =>
    apiClient.post(API_ENDPOINTS.BILLING.PLAN_RESYNC(id)).then(r => r.data),

  // Subscription (current tenant)
  getSubscription: () =>
    apiClient.get<{ data: TenantSubscription }>(API_ENDPOINTS.BILLING.SUBSCRIPTION).then(r => r.data.data),

  changePlan: (data: ChangePlanData) =>
    apiClient.post(API_ENDPOINTS.BILLING.CHANGE_PLAN, data).then(r => r.data),

  cancelSubscription: () =>
    apiClient.post(API_ENDPOINTS.BILLING.CANCEL).then(r => r.data),

  // Payments & Usage
  getPayments: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.BILLING.PAYMENTS, { params }).then(r => r.data),

  getUsage: () =>
    apiClient.get<{ data: Usage }>(API_ENDPOINTS.BILLING.USAGE).then(r => r.data.data),

  // Platform admin: per-tenant subscription
  getTenantSubscription: (tenantId: string) =>
    apiClient
      .get<{ data: TenantSubscription }>(API_ENDPOINTS.BILLING.TENANT_SUBSCRIPTION(tenantId))
      .then(r => r.data.data),

  changeTenantPlan: (tenantId: string, data: ChangePlanData) =>
    apiClient.post(API_ENDPOINTS.BILLING.TENANT_CHANGE_PLAN(tenantId), data).then(r => r.data),
};
