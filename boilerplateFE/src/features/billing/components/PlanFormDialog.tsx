import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronDown, Sparkles } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { useCreatePlan, useUpdatePlan } from '../api';
import { PlanCardPreview } from './PlanCardPreview';
import { PlanFeaturesEditor } from './PlanFeaturesEditor';
import type { PlanFeatureEntry, SubscriptionPlan } from '@/types';

const CURRENCIES = ['USD', 'IQD', 'EUR', 'GBP'] as const;

interface PlanFormState {
  name: string;
  slug: string;
  description: string;
  translations: string;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  features: PlanFeatureEntry[];
  isFree: boolean;
  isPublic: boolean;
  displayOrder: number;
  trialDays: number;
  priceChangeReason: string;
}

const EMPTY_FORM: PlanFormState = {
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
  priceChangeReason: '',
};

function planToForm(plan: SubscriptionPlan): PlanFormState {
  return {
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
  };
}

function slugify(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}

type PlanFormDialogProps =
  | {
      mode: 'create';
      open: boolean;
      onOpenChange: (open: boolean) => void;
      plan?: never;
    }
  | {
      mode: 'edit';
      open: boolean;
      onOpenChange: (open: boolean) => void;
      plan: SubscriptionPlan;
    };

export function PlanFormDialog(props: PlanFormDialogProps) {
  const { mode, open, onOpenChange } = props;
  const { t } = useTranslation();

  const initial = useMemo(
    () => (mode === 'edit' && props.plan ? planToForm(props.plan) : EMPTY_FORM),
    [mode, props.plan],
  );

  const [form, setForm] = useState<PlanFormState>(initial);
  const [showAdvanced, setShowAdvanced] = useState(false);

  const { mutate: createPlan, isPending: isCreating } = useCreatePlan();
  const { mutate: updatePlan, isPending: isUpdating } = useUpdatePlan();
  const isPending = isCreating || isUpdating;

  // Re-seed the form whenever the dialog reopens or the underlying plan changes.
  // The setState calls here are intentional — they reset transient form state
  // when the dialog re-mounts with a different plan or after a save closes it.
  useEffect(() => {
    if (open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setForm(initial);
      setShowAdvanced(false);
    }
  }, [open, initial]);

  const priceChanged =
    mode === 'edit' &&
    !!props.plan &&
    (form.monthlyPrice !== props.plan.monthlyPrice ||
      form.annualPrice !== props.plan.annualPrice);

  const handleChange = <K extends keyof PlanFormState>(field: K, value: PlanFormState[K]) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleGenerateSlug = () => {
    if (!form.name.trim()) return;
    handleChange('slug', slugify(form.name));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (mode === 'create') {
      createPlan(
        {
          name: form.name,
          slug: form.slug,
          description: form.description,
          translations: form.translations || undefined,
          monthlyPrice: form.monthlyPrice,
          annualPrice: form.annualPrice,
          currency: form.currency,
          features: form.features,
          isFree: form.isFree,
          isPublic: form.isPublic,
          displayOrder: form.displayOrder,
          trialDays: form.trialDays,
        },
        { onSuccess: () => onOpenChange(false) },
      );
      return;
    }
    updatePlan(
      {
        id: props.plan.id,
        name: form.name,
        slug: form.slug,
        description: form.description,
        translations: form.translations || undefined,
        monthlyPrice: form.monthlyPrice,
        annualPrice: form.annualPrice,
        currency: form.currency,
        features: form.features,
        isFree: form.isFree,
        isPublic: form.isPublic,
        displayOrder: form.displayOrder,
        trialDays: form.trialDays,
        priceChangeReason: priceChanged ? form.priceChangeReason : '',
      },
      { onSuccess: () => onOpenChange(false) },
    );
  };

  const submitLabel = mode === 'create'
    ? isPending ? t('common.creating') : t('common.create')
    : isPending ? t('common.saving') : t('common.save');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-3xl lg:max-w-5xl max-h-[92vh] overflow-hidden p-0">
        <div className="grid lg:grid-cols-[minmax(0,1fr)_320px] max-h-[92vh]">
          {/* Form column */}
          <form
            onSubmit={handleSubmit}
            className="flex flex-col min-h-0 overflow-y-auto"
          >
            <DialogHeader className="px-6 pt-6 pb-2 shrink-0 text-start">
              <DialogTitle className="gradient-text text-xl font-semibold">
                {mode === 'create' ? t('billing.createPlan') : t('billing.editPlan')}
              </DialogTitle>
              <DialogDescription>
                {mode === 'create' ? t('billing.createPlanDesc') : t('billing.editPlanDesc')}
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-6 px-6 py-4 flex-1 min-h-0">
              {/* Basics ------------------------------------------------------ */}
              <SectionHeader>{t('billing.planForm.basicsSection')}</SectionHeader>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="pf-name">{t('billing.planName')}</Label>
                  <Input
                    id="pf-name"
                    value={form.name}
                    onChange={(e) => handleChange('name', e.target.value)}
                    required
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="pf-slug">{t('billing.planSlug')}</Label>
                  <div className="flex gap-2">
                    <Input
                      id="pf-slug"
                      value={form.slug}
                      onChange={(e) => handleChange('slug', e.target.value)}
                      required
                    />
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={handleGenerateSlug}
                      disabled={!form.name.trim()}
                      title={t('billing.planForm.slugAutoGenerate')}
                    >
                      <Sparkles className="h-3.5 w-3.5 ltr:mr-1.5 rtl:ml-1.5" />
                      {t('billing.planForm.slugAutoGenerate')}
                    </Button>
                  </div>
                </div>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="pf-desc">{t('billing.planDescription')}</Label>
                <Textarea
                  id="pf-desc"
                  rows={2}
                  value={form.description}
                  onChange={(e) => handleChange('description', e.target.value)}
                />
              </div>

              {/* Pricing ----------------------------------------------------- */}
              <SectionHeader>{t('billing.planForm.pricingSection')}</SectionHeader>
              <div className="grid gap-4 sm:grid-cols-3">
                <div className="space-y-1.5">
                  <Label htmlFor="pf-currency">{t('billing.currency')}</Label>
                  <Select
                    value={form.currency}
                    onValueChange={(value) => handleChange('currency', value)}
                  >
                    <SelectTrigger id="pf-currency">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {/* Surface the existing currency even if it's outside the
                       *  default short-list, so editing a plan with an exotic
                       *  currency doesn't silently lose the value. */}
                      {(CURRENCIES.includes(form.currency as typeof CURRENCIES[number])
                        ? CURRENCIES
                        : [form.currency, ...CURRENCIES]
                      ).map((c) => (
                        <SelectItem key={c} value={c}>{c}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="pf-monthly">{t('billing.monthlyPrice')}</Label>
                  <Input
                    id="pf-monthly"
                    type="number"
                    min={0}
                    step="0.01"
                    value={form.monthlyPrice}
                    onChange={(e) => handleChange('monthlyPrice', parseFloat(e.target.value) || 0)}
                    disabled={form.isFree}
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="pf-annual">{t('billing.annualPrice')}</Label>
                  <Input
                    id="pf-annual"
                    type="number"
                    min={0}
                    step="0.01"
                    value={form.annualPrice}
                    onChange={(e) => handleChange('annualPrice', parseFloat(e.target.value) || 0)}
                    disabled={form.isFree}
                  />
                </div>
              </div>

              {priceChanged && (
                <div className="space-y-1.5 rounded-xl border border-warning/40 bg-warning/5 p-3">
                  <Label htmlFor="pf-reason">{t('billing.priceChangeReason')}</Label>
                  <Textarea
                    id="pf-reason"
                    rows={2}
                    value={form.priceChangeReason}
                    onChange={(e) => handleChange('priceChangeReason', e.target.value)}
                    placeholder={t('billing.planForm.priceChangePlaceholder')}
                  />
                </div>
              )}

              {/* Features ---------------------------------------------------- */}
              <SectionHeader>{t('billing.planForm.featuresSection')}</SectionHeader>
              <PlanFeaturesEditor
                value={form.features}
                onChange={(features) => handleChange('features', features)}
              />

              {/* Options ----------------------------------------------------- */}
              <SectionHeader>{t('billing.planForm.optionsSection')}</SectionHeader>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="pf-trial">{t('billing.trialDays')}</Label>
                  <Input
                    id="pf-trial"
                    type="number"
                    min={0}
                    value={form.trialDays}
                    onChange={(e) => handleChange('trialDays', parseInt(e.target.value, 10) || 0)}
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="pf-order">{t('billing.displayOrder')}</Label>
                  <Input
                    id="pf-order"
                    type="number"
                    value={form.displayOrder}
                    onChange={(e) => handleChange('displayOrder', parseInt(e.target.value, 10) || 0)}
                  />
                  <p className="text-[10px] text-muted-foreground/70">
                    {t('billing.planForm.displayOrderHint')}
                  </p>
                </div>
              </div>

              <div className="flex flex-col gap-3 rounded-xl border border-border/60 bg-foreground/[0.02] p-3">
                <label className="flex items-center gap-3 text-sm cursor-pointer select-none">
                  <Checkbox
                    checked={form.isFree}
                    onCheckedChange={(value) => handleChange('isFree', value === true)}
                  />
                  <div>
                    <div className="font-medium">{t('billing.isFree')}</div>
                    <div className="text-xs text-muted-foreground">
                      {t('billing.planForm.isFreeHint')}
                    </div>
                  </div>
                </label>
                <label className="flex items-center gap-3 text-sm cursor-pointer select-none">
                  <Checkbox
                    checked={form.isPublic}
                    onCheckedChange={(value) => handleChange('isPublic', value === true)}
                  />
                  <div>
                    <div className="font-medium">{t('billing.isPublic')}</div>
                    <div className="text-xs text-muted-foreground">
                      {t('billing.planForm.isPublicHint')}
                    </div>
                  </div>
                </label>
              </div>

              {/* Advanced (collapsible) ------------------------------------- */}
              <div className="space-y-2">
                <button
                  type="button"
                  onClick={() => setShowAdvanced((v) => !v)}
                  className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.12em] text-muted-foreground hover:text-foreground motion-safe:transition-colors"
                  aria-expanded={showAdvanced}
                >
                  <ChevronDown
                    className={cn(
                      'h-3.5 w-3.5 motion-safe:transition-transform',
                      showAdvanced && 'rotate-180',
                    )}
                  />
                  {t('billing.planForm.advancedSection')}
                </button>
                {showAdvanced && (
                  <div className="space-y-1.5 pt-1">
                    <Label htmlFor="pf-translations">{t('billing.translations')}</Label>
                    <Textarea
                      id="pf-translations"
                      rows={4}
                      placeholder='{"en": {"name": "..."}, "ar": {"name": "..."}}'
                      value={form.translations}
                      onChange={(e) => handleChange('translations', e.target.value)}
                      className="font-mono text-xs"
                    />
                    <p className="text-[10px] text-muted-foreground/70">
                      {t('billing.planForm.translationsHint')}
                    </p>
                  </div>
                )}
              </div>
            </div>

            {/* Footer ------------------------------------------------------ */}
            <div className="flex items-center justify-end gap-2 border-t border-border/40 bg-background/60 px-6 py-4 shrink-0">
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                {t('common.cancel')}
              </Button>
              <Button type="submit" disabled={isPending}>
                {submitLabel}
              </Button>
            </div>
          </form>

          {/* Preview column ----------------------------------------------- */}
          <aside className="hidden lg:block border-s border-border/40 bg-foreground/[0.02] p-6 overflow-y-auto">
            <PlanCardPreview
              name={form.name}
              description={form.description}
              monthlyPrice={form.monthlyPrice}
              annualPrice={form.annualPrice}
              currency={form.currency}
              features={form.features}
              isFree={form.isFree}
              trialDays={form.trialDays}
            />
          </aside>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function SectionHeader({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground/70 flex items-center gap-1.5">
      <span className="inline-block h-1 w-1 rounded-full bg-primary/70" />
      {children}
    </h3>
  );
}
