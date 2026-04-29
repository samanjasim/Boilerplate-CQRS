import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CreditCard, Receipt } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import { useSubscription, useUsage, usePayments, useCancelSubscription } from '../api';
import { BillingHero } from '../components/BillingHero';
import { PlanSelectorModal } from '../components/PlanSelectorModal';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { formatDate } from '@/utils/format';
import { PAYMENT_STATUS_VARIANT, PAYMENT_STATUS_LABEL } from '../constants/status';

export default function BillingPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [planModalOpen, setPlanModalOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);

  const { data: subscription, isLoading: subLoading } = useSubscription();
  const { data: usage, isLoading: usageLoading } = useUsage();
  const { data: paymentsData, isLoading: paymentsLoading } = usePayments({ pageSize: 20 });
  const cancelMutation = useCancelSubscription();

  const payments = (paymentsData as { data?: unknown[] })?.data ?? (Array.isArray(paymentsData) ? paymentsData : []);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('billing.title')}
        subtitle={t('billing.subtitle')}
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
          action={
            hasPermission(PERMISSIONS.Billing.Manage) && subscription ? (
              <div className="flex flex-wrap items-center gap-2">
                <Button onClick={() => setPlanModalOpen(true)}>
                  {t('billing.changePlan')}
                </Button>
                {subscription.planSlug !== 'free' && subscription.status !== 'Canceled' && (
                  <Button variant="outline" onClick={() => setCancelOpen(true)}>
                    {t('billing.cancelSubscription')}
                  </Button>
                )}
              </div>
            ) : null
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

      {/* Cancel Subscription Confirm */}
      <ConfirmDialog
        isOpen={cancelOpen}
        onClose={() => setCancelOpen(false)}
        onConfirm={() => {
          // Close the dialog on settle — success or failure — so the user always
          // sees the refreshed page state (or the toast error from the axios
          // interceptor) rather than a frozen modal. Success invalidation runs
          // in the hook; the page re-renders via useSubscription's refetch.
          cancelMutation.mutate(undefined, {
            onSettled: () => setCancelOpen(false),
          });
        }}
        title={t('billing.cancelSubscription')}
        description={t('billing.cancelConfirm')}
        confirmLabel={t('billing.cancelSubscription')}
        variant="danger"
        isLoading={cancelMutation.isPending}
      />
    </div>
  );
}
