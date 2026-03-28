import { useState, useEffect } from 'react';
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
import { useUpdateFeatureFlag } from '../api';
import type { FeatureFlagDto } from '../api';

const CATEGORY_OPTIONS = [
  { labelKey: 'featureFlags.categoryUsers', value: 0 },
  { labelKey: 'featureFlags.categoryFiles', value: 1 },
  { labelKey: 'featureFlags.categoryReports', value: 2 },
  { labelKey: 'featureFlags.categoryApiKeys', value: 3 },
  { labelKey: 'featureFlags.categoryBilling', value: 4 },
  { labelKey: 'featureFlags.categorySystem', value: 5 },
  { labelKey: 'featureFlags.categoryCustom', value: 6 },
] as const;

interface EditFeatureFlagDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  flag: FeatureFlagDto | null;
}

export function EditFeatureFlagDialog({ open, onOpenChange, flag }: EditFeatureFlagDialogProps) {
  const { t } = useTranslation();
  const updateMutation = useUpdateFeatureFlag();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [defaultValue, setDefaultValue] = useState('');
  const [category, setCategory] = useState<number>(0);

  const isBooleanType = flag?.valueType === 'Boolean' || (flag?.valueType as unknown) === 0;

  useEffect(() => {
    if (flag) {
      setName(flag.name);
      setDescription(flag.description ?? '');
      setDefaultValue(flag.defaultValue);
      setCategory(flag.category ?? 0);
    }
  }, [flag]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!flag) return;
    await updateMutation.mutateAsync({
      id: flag.id,
      name,
      description: description || null,
      defaultValue,
      category,
    });
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('featureFlags.editTitle')}</DialogTitle>
          <DialogDescription>{t('featureFlags.editDescription', { key: flag?.key })}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="ff-edit-name">{t('featureFlags.name')}</Label>
            <Input
              id="ff-edit-name"
              value={name}
              onChange={e => setName(e.target.value)}
              required
              maxLength={200}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="ff-edit-description">{t('featureFlags.descriptionLabel')}</Label>
            <Textarea
              id="ff-edit-description"
              value={description}
              onChange={e => setDescription(e.target.value)}
              rows={3}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="ff-edit-default">{t('featureFlags.defaultValue')}</Label>
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
                id="ff-edit-default"
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

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              disabled={!name || !defaultValue || updateMutation.isPending}
            >
              {updateMutation.isPending ? t('common.loading') : t('common.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
