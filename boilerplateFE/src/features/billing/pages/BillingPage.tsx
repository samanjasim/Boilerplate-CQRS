import { useState } from 'react';
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
import { useSubscription, useUsage, usePayments } from '../api';
import { UsageBar } from '../components/UsageBar';
import { PlanSelectorModal } from '../components/PlanSelectorModal';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { STATUS_BADGE_VARIANT } from '@/constants';
import { formatDate, formatFileSize } from '@/utils/format';
import { PAYMENT_STATUS_VARIANT, PAYMENT_STATUS_LABEL, SUBSCRIPTION_STATUS, BILLING_INTERVAL } from '../constants/status';

export default function BillingPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [planModalOpen, setPlanModalOpen] = useState(false);

  const { data: subscription, isLoading: subLoading } = useSubscription();
  const { data: usage, isLoading: usageLoading } = useUsage();
  const { data: paymentsData, isLoading: paymentsLoading } = usePayments({ pageSize: 20 });

  const payments = (paymentsData as { data?: unknown[] })?.data ?? (Array.isArray(paymentsData) ? paymentsData : []);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('billing.title')}
        subtitle={t('billing.subtitle')}
      />

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
                    ).toFixed(2)}
                    {BILLING_INTERVAL[subscription.billingInterval] === 'Monthly' ? t('billing.perMonth') : t('billing.perYear')}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {formatDate(subscription.currentPeriodStart)} – {formatDate(subscription.currentPeriodEnd)}
                  </p>
                </div>

                {hasPermission(PERMISSIONS.Billing.Manage) && (
                  <Button onClick={() => setPlanModalOpen(true)}>
                    {t('billing.changePlan')}
                  </Button>
                )}
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
              {(payments as import('@/types').PaymentRecord[]).map((p) => (
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

      {/* Plan Selector Modal */}
      <PlanSelectorModal
        open={planModalOpen}
        onOpenChange={setPlanModalOpen}
        subscription={subscription}
      />
    </div>
  );
}
