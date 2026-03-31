import { useState } from 'react';
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
import { useCreatePlan } from '../api';
import { PlanFeaturesEditor } from './PlanFeaturesEditor';
import type { CreatePlanData } from '@/types';

interface CreatePlanDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const DEFAULT_FORM: CreatePlanData = {
  name: '',
  slug: '',
  description: '',
  translations: '',
  monthlyPrice: 0,
  annualPrice: 0,
  currency: 'USD',
  features: [],
  isFree: false,
  isPublic: true,
  displayOrder: 0,
  trialDays: 0,
};

export function CreatePlanDialog({ open, onOpenChange }: CreatePlanDialogProps) {
  const { t } = useTranslation();
  const [form, setForm] = useState<CreatePlanData>(DEFAULT_FORM);
  const { mutate: createPlan, isPending } = useCreatePlan();

  const handleChange = (field: keyof CreatePlanData, value: unknown) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createPlan(form, {
      onSuccess: () => {
        setForm(DEFAULT_FORM);
        onOpenChange(false);
      },
    });
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) setForm(DEFAULT_FORM);
    onOpenChange(isOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('billing.createPlan')}</DialogTitle>
          <DialogDescription>{t('billing.createPlanDesc')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="cp-name">{t('billing.planName')}</Label>
              <Input
                id="cp-name"
                value={form.name}
                onChange={(e) => handleChange('name', e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cp-slug">{t('billing.planSlug')}</Label>
              <Input
                id="cp-slug"
                value={form.slug}
                onChange={(e) => handleChange('slug', e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cp-currency">{t('billing.currency')}</Label>
              <Input
                id="cp-currency"
                value={form.currency}
                onChange={(e) => handleChange('currency', e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cp-monthly">{t('billing.monthlyPrice')}</Label>
              <Input
                id="cp-monthly"
                type="number"
                min={0}
                step="0.01"
                value={form.monthlyPrice}
                onChange={(e) => handleChange('monthlyPrice', parseFloat(e.target.value) || 0)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cp-annual">{t('billing.annualPrice')}</Label>
              <Input
                id="cp-annual"
                type="number"
                min={0}
                step="0.01"
                value={form.annualPrice}
                onChange={(e) => handleChange('annualPrice', parseFloat(e.target.value) || 0)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cp-trial">{t('billing.trialDays')}</Label>
              <Input
                id="cp-trial"
                type="number"
                min={0}
                value={form.trialDays}
                onChange={(e) => handleChange('trialDays', parseInt(e.target.value, 10) || 0)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cp-order">{t('billing.displayOrder')}</Label>
              <Input
                id="cp-order"
                type="number"
                value={form.displayOrder}
                onChange={(e) => handleChange('displayOrder', parseInt(e.target.value, 10) || 0)}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="cp-desc">{t('billing.planDescription')}</Label>
            <Textarea
              id="cp-desc"
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
            <Label htmlFor="cp-translations">{t('billing.translations')}</Label>
            <Textarea
              id="cp-translations"
              rows={3}
              placeholder='{"en": {"name": "..."}, "ar": {"name": "..."}}'
              value={form.translations ?? ''}
              onChange={(e) => handleChange('translations', e.target.value)}
            />
          </div>

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
            <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? t('common.creating') : t('common.create')}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
