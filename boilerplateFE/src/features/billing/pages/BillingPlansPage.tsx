import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, CreditCard, Pencil, RefreshCw, Trash2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import { useAllPlans, useDeactivatePlan, useResyncPlan } from '../api';
import { CreatePlanDialog } from '../components/CreatePlanDialog';
import { EditPlanDialog } from '../components/EditPlanDialog';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { SubscriptionPlan, PlanFeatureEntry } from '@/types';

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
            <Button onClick={() => setCreateOpen(true)}>
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
      <CreatePlanDialog open={createOpen} onOpenChange={setCreateOpen} />

      {editPlan && (
        <EditPlanDialog
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

function getFeatureHighlights(features: PlanFeatureEntry[]): string[] {
  if (!Array.isArray(features) || features.length === 0) return [];
  return features
    .filter((f) => !(f.value === 'false'))
    .slice(0, 6)
    .map((f) => {
      const label = f.translations?.en?.label;
      if (label) return label;
      // Fallback: humanize the key
      return f.key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()).trim();
    });
}

function PlanCard({ plan, canManage, onEdit, onDeactivate, onResync }: PlanCardProps) {
  const { t } = useTranslation();
  const features = getFeatureHighlights(plan.features);

  return (
    <Card className="flex flex-col">
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
        <div className="flex items-center gap-3 text-sm">
          <span className="text-foreground font-medium">
            {plan.isFree ? (
              <Badge variant="outline">{t('billing.freeLabel')}</Badge>
            ) : (
              <>
                {plan.currency} {plan.monthlyPrice.toFixed(2)}{t('billing.perMonth')}
                <span className="mx-1 text-muted-foreground">·</span>
                {plan.currency} {plan.annualPrice.toFixed(2)}{t('billing.perYear')}
              </>
            )}
          </span>
        </div>

        {/* Subscribers */}
        <p className="text-xs text-muted-foreground">
          {t('billing.subscribers')}: <span className="font-medium text-foreground">{plan.subscriberCount}</span>
        </p>

        {/* Feature highlights */}
        {features.length > 0 && (
          <ul className="space-y-0.5 flex-1">
            {features.map((f, i) => (
              <li key={i} className="text-xs text-muted-foreground">• {f}</li>
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
