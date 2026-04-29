import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Plus, CreditCard, Pencil, RefreshCw, Trash2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import { useAllPlans, useDeactivatePlan, useResyncPlan } from '../api';
import { PlanFormDialog } from '../components/PlanFormDialog';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { getFeatureHighlights } from '../utils/features';
import type { SubscriptionPlan } from '@/types';

export default function BillingPlansPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [createOpen, setCreateOpen] = useState(false);
  const [editPlan, setEditPlan] = useState<SubscriptionPlan | null>(null);
  const [deactivateId, setDeactivateId] = useState<string | null>(null);
  const [resyncId, setResyncId] = useState<string | null>(null);

  const { data: plansData, isLoading, isError } = useAllPlans();
  const { mutate: deactivatePlan, isPending: isDeactivating } = useDeactivatePlan();
  const { mutate: resyncPlan, isPending: isResyncing } = useResyncPlan();

  const plans: SubscriptionPlan[] = (plansData as { data?: SubscriptionPlan[] })?.data ??
    (Array.isArray(plansData) ? (plansData as SubscriptionPlan[]) : []);

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('billing.plans')} subtitle={t('billing.plansSubtitle')} />
        <EmptyState icon={CreditCard} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading && !plansData) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  const deactivatePlanName = plans.find((p) => p.id === deactivateId)?.name ?? '';
  const resyncPlanName = plans.find((p) => p.id === resyncId)?.name ?? '';

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('billing.plans')}
        subtitle={t('billing.plansSubtitle')}
        actions={
          hasPermission(PERMISSIONS.Billing.ManagePlans) ? (
            <Button onClick={() => setCreateOpen(true)} className="btn-primary-gradient">
              <Plus className="mr-2 h-4 w-4" />
              {t('billing.createPlan')}
            </Button>
          ) : undefined
        }
      />

      {plans.length === 0 ? (
        <EmptyState
          icon={CreditCard}
          title={t('billing.plans')}
          description={t('billing.plansSubtitle')}
          action={
            hasPermission(PERMISSIONS.Billing.ManagePlans)
              ? { label: t('billing.createPlan'), onClick: () => setCreateOpen(true) }
              : undefined
          }
        />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {plans.map((plan) => (
            <PlanCard
              key={plan.id}
              plan={plan}
              canManage={hasPermission(PERMISSIONS.Billing.ManagePlans)}
              onEdit={() => setEditPlan(plan)}
              onDeactivate={() => setDeactivateId(plan.id)}
              onResync={() => setResyncId(plan.id)}
            />
          ))}
        </div>
      )}

      {/* Dialogs */}
      <PlanFormDialog mode="create" open={createOpen} onOpenChange={setCreateOpen} />

      {editPlan && (
        <PlanFormDialog
          mode="edit"
          open={!!editPlan}
          onOpenChange={(open) => { if (!open) setEditPlan(null); }}
          plan={editPlan}
        />
      )}

      <ConfirmDialog
        isOpen={!!deactivateId}
        onClose={() => setDeactivateId(null)}
        onConfirm={() => {
          if (deactivateId) {
            deactivatePlan(deactivateId, { onSuccess: () => setDeactivateId(null) });
          }
        }}
        title={t('billing.deactivatePlan')}
        description={t('billing.deactivatePlanConfirm', { name: deactivatePlanName })}
        confirmLabel={t('billing.deactivatePlan')}
        isLoading={isDeactivating}
      />

      <ConfirmDialog
        isOpen={!!resyncId}
        onClose={() => setResyncId(null)}
        onConfirm={() => {
          if (resyncId) {
            resyncPlan(resyncId, { onSuccess: () => setResyncId(null) });
          }
        }}
        title={t('billing.resync')}
        description={t('billing.resyncPlanConfirm', { name: resyncPlanName })}
        confirmLabel={t('billing.resync')}
        variant="primary"
        isLoading={isResyncing}
      />
    </div>
  );
}

interface PlanCardProps {
  plan: SubscriptionPlan;
  canManage: boolean;
  onEdit: () => void;
  onDeactivate: () => void;
  onResync: () => void;
}

function PlanCard({ plan, canManage, onEdit, onDeactivate, onResync }: PlanCardProps) {
  const { t } = useTranslation();
  const features = getFeatureHighlights(plan.features);

  return (
    <Card variant="glass" className="flex flex-col">
      <CardContent className="py-5 flex flex-col gap-3 flex-1">
        {/* Header */}
        <div className="flex items-start justify-between gap-2">
          <div>
            <h3 className="font-semibold text-foreground">{plan.name}</h3>
            <code className="text-xs text-muted-foreground bg-secondary rounded px-1.5 py-0.5">
              {plan.slug}
            </code>
          </div>
          <Badge variant={plan.isActive ? 'default' : 'secondary'}>
            {plan.isActive ? t('common.active') : t('common.inactive')}
          </Badge>
        </div>

        {/* Prices */}
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-sm">
          {plan.isFree ? (
            <span className="text-2xl font-semibold tabular-nums gradient-text">
              {t('billing.freeLabel')}
            </span>
          ) : (
            <>
              <span>
                <span className="text-2xl font-semibold tabular-nums gradient-text">
                  {plan.currency} {plan.monthlyPrice.toFixed(2)}
                </span>
                <span className="text-muted-foreground">{t('billing.perMonth')}</span>
              </span>
              <span className="text-muted-foreground">·</span>
              <span>
                <span className="text-2xl font-semibold tabular-nums gradient-text">
                  {plan.currency} {plan.annualPrice.toFixed(2)}
                </span>
                <span className="text-muted-foreground">{t('billing.perYear')}</span>
              </span>
            </>
          )}
        </div>

        {/* Subscribers */}
        <div>
          <div className="text-[10px] uppercase tracking-[0.12em] text-muted-foreground/70">
            {t('billing.subscribers')}
          </div>
          <div className="text-sm font-medium tabular-nums text-foreground">{plan.subscriberCount}</div>
        </div>

        {/* Feature highlights */}
        {features.length > 0 && (
          <ul className="space-y-0.5 flex-1">
            {features.map((f, i) => (
              <li key={i} className="flex items-start gap-2 text-xs">
                <Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-primary" />
                <span className="text-muted-foreground">{f}</span>
              </li>
            ))}
          </ul>
        )}

        {/* Actions */}
        {canManage && (
          <div className="flex items-center gap-1.5 pt-2 border-t">
            <Button variant="ghost" size="sm" onClick={onEdit}>
              <Pencil className="h-3.5 w-3.5" />
              {t('common.edit')}
            </Button>
            <Button variant="ghost" size="sm" onClick={onResync}>
              <RefreshCw className="h-3.5 w-3.5" />
              {t('billing.resync')}
            </Button>
            {plan.isActive && (
              <Button variant="ghost" size="sm" onClick={onDeactivate} className="text-destructive hover:text-destructive">
                <Trash2 className="h-3.5 w-3.5" />
                {t('billing.deactivatePlan')}
              </Button>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
