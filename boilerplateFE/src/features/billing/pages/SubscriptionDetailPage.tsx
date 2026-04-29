import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CreditCard, Receipt } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState } from '@/components/common';
import { useTenantSubscription, useTenantUsage, useTenantPayments, useChangeTenantPlan, usePlans } from '../api';
import { BillingHero } from '../components/BillingHero';
import { useTenant } from '@/features/tenants/api';
import { ROUTES } from '@/config';
import { formatDate } from '@/utils/format';
import { PAYMENT_STATUS_VARIANT, PAYMENT_STATUS_LABEL } from '../constants/status';
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

  const [planModalOpen, setPlanModalOpen] = useState(false);
  const [selectedPlanId, setSelectedPlanId] = useState<string>('');

  const { data: tenant } = useTenant(tenantId ?? '');
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

  const headerTitle = tenant
    ? t('billing.tenantSubscription', { tenantName: tenant.name })
    : t('billing.subscriptionDetail');

  return (
    <div className="space-y-6">
      <PageHeader
        title={headerTitle}
        breadcrumbs={[
          { to: ROUTES.SUBSCRIPTIONS.LIST, label: t('billing.subscriptions') },
          { label: tenant?.name ?? subscription?.tenantId ?? t('common.loading') },
        ]}
      />

      {!subLoading && !subscription ? (
        <EmptyState
          icon={CreditCard}
          title={t('billing.currentPlan')}
          description={t('billing.subtitle')}
        />
      ) : (
        <BillingHero
          subscription={subscription}
          usage={usage}
          isLoading={subLoading || usageLoading}
          eyebrow={tenant?.name ?? subscription?.tenantId}
          action={
            <Button
              onClick={() => {
                if (subscription) setSelectedPlanId(subscription.subscriptionPlanId);
                setPlanModalOpen(true);
              }}
              disabled={!subscription}
            >
              {t('billing.changePlan')}
            </Button>
          }
        />
      )}

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
