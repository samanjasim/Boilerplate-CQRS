import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CreditCard, Receipt, BarChart3 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState } from '@/components/common';
import { useTenantSubscription, useTenantUsage, useTenantPayments, useChangeTenantPlan, usePlans } from '../api';
import { UsageBar } from '../components/UsageBar';
import { useBackNavigation } from '@/hooks';
import { ROUTES } from '@/config';
import { STATUS_BADGE_VARIANT } from '@/constants';
import { formatDate, formatFileSize } from '@/utils/format';
import { PAYMENT_STATUS_VARIANT, PAYMENT_STATUS_LABEL, SUBSCRIPTION_STATUS, BILLING_INTERVAL } from '../constants/status';
import type { SubscriptionPlan, PaymentRecord } from '@/types';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';

export default function SubscriptionDetailPage() {
  const { t } = useTranslation();
  const { tenantId } = useParams<{ tenantId: string }>();
  useBackNavigation(ROUTES.SUBSCRIPTIONS.LIST, t('billing.subscriptions'));

  const [planModalOpen, setPlanModalOpen] = useState(false);
  const [selectedPlanId, setSelectedPlanId] = useState<string>('');

  const { data: subscription, isLoading: subLoading } = useTenantSubscription(tenantId ?? '');

  const { data: usage, isLoading: usageLoading } = useTenantUsage(tenantId ?? '');
  const { data: paymentsData, isLoading: paymentsLoading } = useTenantPayments(tenantId ?? '', { pageSize: 20 });

  const payments: PaymentRecord[] = (paymentsData as { data?: PaymentRecord[] })?.data ??
    (Array.isArray(paymentsData) ? (paymentsData as PaymentRecord[]) : []);

  const { data: plansData } = usePlans({ pageSize: 50 });
  const { mutate: changePlan, isPending: isChanging } = useChangeTenantPlan();

  const plans: SubscriptionPlan[] = (plansData as { data?: SubscriptionPlan[] })?.data ??
    (Array.isArray(plansData) ? (plansData as SubscriptionPlan[]) : []);

  const handleChangePlan = () => {
    if (tenantId && selectedPlanId) {
      changePlan(
        { tenantId, data: { planId: selectedPlanId } },
        {
          onSuccess: () => {
            setPlanModalOpen(false);
            setSelectedPlanId('');
          },
        },
      );
    }
  };

  const headerTitle = subscription
    ? t('billing.tenantSubscription', { tenantName: subscription.planName ?? '' })
    : t('billing.subscriptionDetail');

  return (
    <div className="space-y-6">
      <PageHeader title={headerTitle} />

      {/* Current Plan */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">{t('billing.currentPlan')}</h2>
        {subLoading ? (
          <div className="flex justify-center py-8">
            <Spinner size="lg" />
          </div>
        ) : !subscription ? (
          <EmptyState
            icon={CreditCard}
            title={t('billing.currentPlan')}
            description={t('billing.subtitle')}
          />
        ) : (
          <Card>
            <CardContent className="py-5">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="space-y-1.5">
                  <div className="flex items-center gap-2">
                    <h3 className="text-lg font-bold text-foreground">{subscription.planName}</h3>
                    <Badge variant={STATUS_BADGE_VARIANT[SUBSCRIPTION_STATUS[subscription.status] ?? subscription.status] ?? 'secondary'}>
                      {SUBSCRIPTION_STATUS[subscription.status] ?? subscription.status}
                    </Badge>
                  </div>
                  <p className="text-sm text-muted-foreground">
                    {BILLING_INTERVAL[subscription.billingInterval] ?? subscription.billingInterval}
                    {' · '}
                    {subscription.currency}{' '}
                    {(BILLING_INTERVAL[subscription.billingInterval] === 'Monthly'
                      ? subscription.lockedMonthlyPrice
                      : subscription.lockedAnnualPrice
                    )?.toFixed(2)}
                    {BILLING_INTERVAL[subscription.billingInterval] === 'Monthly' ? t('billing.perMonth') : t('billing.perYear')}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {formatDate(subscription.currentPeriodStart)} – {formatDate(subscription.currentPeriodEnd)}
                  </p>
                </div>

                <Button onClick={() => { setSelectedPlanId(subscription.subscriptionPlanId); setPlanModalOpen(true); }}>
                  {t('billing.changePlan')}
                </Button>
              </div>
            </CardContent>
          </Card>
        )}
      </section>

      {/* Usage */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">{t('billing.usage')}</h2>
        {usageLoading ? (
          <div className="flex justify-center py-8">
            <Spinner size="lg" />
          </div>
        ) : !usage ? (
          <EmptyState icon={BarChart3} title={t('billing.usage')} />
        ) : (
          <Card>
            <CardContent className="py-5 grid gap-5 sm:grid-cols-2">
              <UsageBar
                label={t('billing.usersUsage')}
                current={usage.users}
                max={usage.maxUsers}
              />
              <UsageBar
                label={t('billing.storageUsage')}
                current={usage.storageBytes}
                max={usage.maxStorageBytes}
                formatValue={formatFileSize}
              />
              <UsageBar
                label={t('billing.apiKeysUsage')}
                current={usage.apiKeys}
                max={usage.maxApiKeys}
              />
              <UsageBar
                label={t('billing.usage') + ' — Reports'}
                current={usage.reportsActive}
                max={usage.maxReports}
              />
              <UsageBar
                label={t('billing.webhooksUsage')}
                current={usage.webhooks}
                max={usage.maxWebhooks}
              />
            </CardContent>
          </Card>
        )}
      </section>

      {/* Payment History */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">{t('billing.payments')}</h2>
        {paymentsLoading ? (
          <div className="flex justify-center py-8">
            <Spinner size="lg" />
          </div>
        ) : payments.length === 0 ? (
          <EmptyState
            icon={Receipt}
            title={t('billing.noPayments')}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('billing.paymentDescription')}</TableHead>
                <TableHead>{t('billing.paymentAmount')}</TableHead>
                <TableHead>{t('common.status')}</TableHead>
                <TableHead>{t('billing.paymentPeriod')}</TableHead>
                <TableHead>{t('common.createdAt')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {payments.map((p) => (
                <TableRow key={p.id}>
                  <TableCell className="text-foreground">{p.description ?? '—'}</TableCell>
                  <TableCell className="font-medium text-foreground">
                    {p.currency} {p.amount.toFixed(2)}
                  </TableCell>
                  <TableCell>
                    <Badge variant={PAYMENT_STATUS_VARIANT[p.status] ?? 'secondary'}>
                      {PAYMENT_STATUS_LABEL[p.status] ?? p.status}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {formatDate(p.periodStart)} – {formatDate(p.periodEnd)}
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {formatDate(p.createdAt)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </section>

      {/* Change Plan Dialog */}
      <Dialog open={planModalOpen} onOpenChange={setPlanModalOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('billing.changePlan')}</DialogTitle>
            <DialogDescription>
              {subscription ? t('billing.changePlanConfirmDesc') : ''}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <Select value={selectedPlanId} onValueChange={setSelectedPlanId}>
              <SelectTrigger>
                <SelectValue placeholder={t('billing.selectPlan')} />
              </SelectTrigger>
              <SelectContent>
                {plans
                  .filter((p) => p.isActive)
                  .map((plan) => (
                    <SelectItem key={plan.id} value={plan.id}>
                      {plan.name} — {plan.isFree ? t('billing.freeLabel') : `${plan.currency} ${plan.monthlyPrice.toFixed(2)}${t('billing.perMonth')}`}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setPlanModalOpen(false)}>
                {t('common.cancel')}
              </Button>
              <Button
                onClick={handleChangePlan}
                disabled={!selectedPlanId || selectedPlanId === subscription?.subscriptionPlanId || isChanging}
              >
                {isChanging ? <Spinner size="sm" className="mr-2" /> : null}
                {t('billing.changePlan')}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
