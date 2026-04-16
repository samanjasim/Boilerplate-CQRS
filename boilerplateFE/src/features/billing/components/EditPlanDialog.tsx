import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useUpdatePlan } from '../api';
import { PlanFeaturesEditor } from './PlanFeaturesEditor';
import type { SubscriptionPlan, UpdatePlanData } from '@/types';

interface EditPlanDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  plan: SubscriptionPlan;
}

export function EditPlanDialog({ open, onOpenChange, plan }: EditPlanDialogProps) {
  const { t } = useTranslation();
  const { mutate: updatePlan, isPending } = useUpdatePlan();

  const [form, setForm] = useState<UpdatePlanData>({
    id: plan.id,
    name: plan.name,
    slug: plan.slug,
    description: plan.description ?? '',
    translations: plan.translations ?? '',
    monthlyPrice: plan.monthlyPrice,
    annualPrice: plan.annualPrice,
    currency: plan.currency,
    features: plan.features,
    isFree: plan.isFree,
    isPublic: plan.isPublic,
    displayOrder: plan.displayOrder,
    trialDays: plan.trialDays,
    priceChangeReason: '',
  });

  const priceChanged =
    form.monthlyPrice !== plan.monthlyPrice || form.annualPrice !== plan.annualPrice;

  // Sync form when plan prop changes
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({
      id: plan.id,
      name: plan.name,
      slug: plan.slug,
      description: plan.description ?? '',
      translations: plan.translations ?? '',
      monthlyPrice: plan.monthlyPrice,
      annualPrice: plan.annualPrice,
      currency: plan.currency,
      features: plan.features,
      isFree: plan.isFree,
      isPublic: plan.isPublic,
      displayOrder: plan.displayOrder,
      trialDays: plan.trialDays,
      priceChangeReason: '',
    });
  }, [plan]);

  const handleChange = (field: keyof UpdatePlanData, value: unknown) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    updatePlan(form, { onSuccess: () => onOpenChange(false) });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('billing.editPlan')}</DialogTitle>
          <DialogDescription>{t('billing.editPlanDesc')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="ep-name">{t('billing.planName')}</Label>
              <Input
                id="ep-name"
                value={form.name}
                onChange={(e) => handleChange('name', e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ep-slug">{t('billing.planSlug')}</Label>
              <Input
                id="ep-slug"
                value={form.slug}
                onChange={(e) => handleChange('slug', e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ep-currency">{t('billing.currency')}</Label>
              <Input
                id="ep-currency"
                value={form.currency}
                onChange={(e) => handleChange('currency', e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ep-monthly">{t('billing.monthlyPrice')}</Label>
              <Input
                id="ep-monthly"
                type="number"
                min={0}
                step="0.01"
                value={form.monthlyPrice}
                onChange={(e) => handleChange('monthlyPrice', parseFloat(e.target.value) || 0)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ep-annual">{t('billing.annualPrice')}</Label>
              <Input
                id="ep-annual"
                type="number"
                min={0}
                step="0.01"
                value={form.annualPrice}
                onChange={(e) => handleChange('annualPrice', parseFloat(e.target.value) || 0)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ep-trial">{t('billing.trialDays')}</Label>
              <Input
                id="ep-trial"
                type="number"
                min={0}
                value={form.trialDays}
                onChange={(e) => handleChange('trialDays', parseInt(e.target.value, 10) || 0)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ep-order">{t('billing.displayOrder')}</Label>
              <Input
                id="ep-order"
                type="number"
                value={form.displayOrder}
                onChange={(e) => handleChange('displayOrder', parseInt(e.target.value, 10) || 0)}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="ep-desc">{t('billing.planDescription')}</Label>
            <Textarea
              id="ep-desc"
              rows={2}
              value={form.description}
              onChange={(e) => handleChange('description', e.target.value)}
            />
          </div>

          <PlanFeaturesEditor
            value={form.features}
            onChange={(f) => handleChange('features', f)}
          />

          <div className="space-y-2">
            <Label htmlFor="ep-translations">{t('billing.translations')}</Label>
            <Textarea
              id="ep-translations"
              rows={3}
              value={form.translations ?? ''}
              onChange={(e) => handleChange('translations', e.target.value)}
            />
          </div>

          {/* Price change reason — only visible when price has changed */}
          {priceChanged && (
            <div className="space-y-2 rounded-xl border border-warning/40 bg-warning/5 p-3">
              <Label htmlFor="ep-reason">{t('billing.priceChangeReason')}</Label>
              <Textarea
                id="ep-reason"
                rows={2}
                value={form.priceChangeReason ?? ''}
                onChange={(e) => handleChange('priceChangeReason', e.target.value)}
                placeholder="Explain why the price changed..."
              />
            </div>
          )}

          <div className="flex items-center gap-6">
            <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
              <input
                type="checkbox"
                checked={form.isFree}
                onChange={(e) => handleChange('isFree', e.target.checked)}
                className="accent-primary"
              />
              {t('billing.isFree')}
            </label>
            <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
              <input
                type="checkbox"
                checked={form.isPublic}
                onChange={(e) => handleChange('isPublic', e.target.checked)}
                className="accent-primary"
              />
              {t('billing.isPublic')}
            </label>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? t('common.saving') : t('common.save')}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
