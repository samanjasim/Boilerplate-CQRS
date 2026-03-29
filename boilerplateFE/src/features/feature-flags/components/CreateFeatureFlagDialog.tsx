import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useCreateFeatureFlag } from '../api';

const VALUE_TYPES = [
  { label: 'Boolean', value: 0 },
  { label: 'String', value: 1 },
  { label: 'Integer', value: 2 },
  { label: 'Json', value: 3 },
] as const;

const VALUE_TYPE_LABELS: Record<number, string> = {
  0: 'Boolean',
  1: 'String',
  2: 'Integer',
  3: 'Json',
};

const CATEGORY_OPTIONS = [
  { labelKey: 'featureFlags.categoryUsers', value: 0 },
  { labelKey: 'featureFlags.categoryFiles', value: 1 },
  { labelKey: 'featureFlags.categoryReports', value: 2 },
  { labelKey: 'featureFlags.categoryApiKeys', value: 3 },
  { labelKey: 'featureFlags.categoryBilling', value: 4 },
  { labelKey: 'featureFlags.categorySystem', value: 5 },
  { labelKey: 'featureFlags.categoryCustom', value: 6 },
] as const;

interface CreateFeatureFlagDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CreateFeatureFlagDialog({ open, onOpenChange }: CreateFeatureFlagDialogProps) {
  const { t } = useTranslation();
  const createMutation = useCreateFeatureFlag();

  const [key, setKey] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [valueType, setValueType] = useState<number>(0);
  const [defaultValue, setDefaultValue] = useState('false');
  const [category, setCategory] = useState<number>(0);
  const [isSystem, setIsSystem] = useState(false);

  const isBooleanType = VALUE_TYPE_LABELS[valueType] === 'Boolean';

  const resetForm = () => {
    setKey('');
    setName('');
    setDescription('');
    setValueType(0);
    setDefaultValue('false');
    setCategory(0);
    setIsSystem(false);
  };

  const handleValueTypeChange = (val: string) => {
    const numVal = Number(val);
    setValueType(numVal);
    if (VALUE_TYPE_LABELS[numVal] === 'Boolean') {
      setDefaultValue('false');
    } else {
      setDefaultValue('');
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await createMutation.mutateAsync({
      key,
      name,
      description: description || null,
      defaultValue,
      valueType,
      category,
      isSystem,
    });
    resetForm();
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={(val) => { if (!val) resetForm(); onOpenChange(val); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('featureFlags.createTitle')}</DialogTitle>
          <DialogDescription>{t('featureFlags.createDescription')}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="ff-key">{t('featureFlags.key')}</Label>
            <Input
              id="ff-key"
              value={key}
              onChange={e => setKey(e.target.value)}
              placeholder="billing.enabled"
              required
              maxLength={200}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="ff-name">{t('featureFlags.name')}</Label>
            <Input
              id="ff-name"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder={t('featureFlags.namePlaceholder')}
              required
              maxLength={200}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="ff-description">{t('featureFlags.descriptionLabel')}</Label>
            <Textarea
              id="ff-description"
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder={t('featureFlags.descriptionPlaceholder')}
              rows={3}
            />
          </div>

          <div className="space-y-2">
            <Label>{t('featureFlags.type')}</Label>
            <Select value={String(valueType)} onValueChange={handleValueTypeChange}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {VALUE_TYPES.map(vt => (
                  <SelectItem key={vt.value} value={String(vt.value)}>
                    {vt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="ff-default">{t('featureFlags.defaultValue')}</Label>
            {isBooleanType ? (
              <div className="flex items-center gap-3">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={defaultValue === 'true'}
                    onChange={e => setDefaultValue(e.target.checked ? 'true' : 'false')}
                    className="h-4 w-4 rounded border-border accent-primary"
                  />
                  <span className="text-sm text-foreground">
                    {defaultValue === 'true' ? t('featureFlags.enabled') : t('featureFlags.disabled')}
                  </span>
                </label>
              </div>
            ) : (
              <Input
                id="ff-default"
                value={defaultValue}
                onChange={e => setDefaultValue(e.target.value)}
                required
              />
            )}
          </div>

          <div className="space-y-2">
            <Label>{t('featureFlags.category')}</Label>
            <Select value={String(category)} onValueChange={val => setCategory(Number(val))}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CATEGORY_OPTIONS.map(opt => (
                  <SelectItem key={opt.value} value={String(opt.value)}>
                    {t(opt.labelKey)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-center gap-2">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={isSystem}
                onChange={e => setIsSystem(e.target.checked)}
                className="h-4 w-4 rounded border-border accent-primary"
              />
              <span className="text-sm text-foreground">{t('featureFlags.isSystem')}</span>
            </label>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              disabled={!key || !name || !defaultValue || createMutation.isPending}
            >
              {createMutation.isPending ? t('common.loading') : t('featureFlags.create')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
