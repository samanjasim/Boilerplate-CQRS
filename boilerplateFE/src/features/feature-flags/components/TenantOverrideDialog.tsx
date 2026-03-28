import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { useSetTenantOverride, useRemoveTenantOverride } from '../api';
import type { FeatureFlagDto } from '../api';
import { useAuthStore, selectUser } from '@/stores';
import { useTenants } from '@/features/tenants/api';

interface TenantOverrideDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  flag: FeatureFlagDto | null;
}

export function TenantOverrideDialog({ open, onOpenChange, flag }: TenantOverrideDialogProps) {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const setOverrideMutation = useSetTenantOverride();
  const removeOverrideMutation = useRemoveTenantOverride();

  const isPlatformAdmin = !user?.tenantId;
  const { data: tenantsData } = useTenants(
    isPlatformAdmin ? { pageNumber: 1, pageSize: 100 } : undefined
  );

  const [tenantId, setTenantId] = useState('');
  const [overrideValue, setOverrideValue] = useState('');

  const isBooleanType = flag?.valueType === 'Boolean';

  useEffect(() => {
    if (flag) {
      setOverrideValue(flag.tenantOverrideValue ?? flag.defaultValue);
      if (!isPlatformAdmin && user?.tenantId) {
        setTenantId(user.tenantId);
      } else {
        setTenantId('');
      }
    }
  }, [flag, isPlatformAdmin, user?.tenantId]);

  const handleSetOverride = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!flag || !tenantId) return;
    await setOverrideMutation.mutateAsync({
      flagId: flag.id,
      tenantId,
      data: { value: overrideValue },
    });
    onOpenChange(false);
  };

  const handleRemoveOverride = async () => {
    if (!flag || !tenantId) return;
    await removeOverrideMutation.mutateAsync({
      flagId: flag.id,
      tenantId,
    });
    onOpenChange(false);
  };

  const tenants = tenantsData?.data ?? [];
  const isLoading = setOverrideMutation.isPending || removeOverrideMutation.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('featureFlags.overrideTitle')}</DialogTitle>
          <DialogDescription>{t('featureFlags.overrideDescription', { key: flag?.key })}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSetOverride} className="space-y-4">
          <div className="space-y-2">
            <Label>{t('featureFlags.defaultValue')}</Label>
            <p className="text-sm text-muted-foreground">{flag?.defaultValue}</p>
          </div>

          {flag?.tenantOverrideValue !== null && (
            <div className="space-y-2">
              <Label>{t('featureFlags.currentOverride')}</Label>
              <p className="text-sm text-foreground">{flag?.tenantOverrideValue}</p>
            </div>
          )}

          {isPlatformAdmin && (
            <div className="space-y-2">
              <Label>{t('featureFlags.tenant')}</Label>
              <Select value={tenantId} onValueChange={setTenantId}>
                <SelectTrigger>
                  <SelectValue placeholder={t('featureFlags.selectTenant')} />
                </SelectTrigger>
                <SelectContent>
                  {tenants.map((tenant: { id: string; name: string }) => (
                    <SelectItem key={tenant.id} value={tenant.id}>
                      {tenant.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="ff-override-value">{t('featureFlags.overrideValue')}</Label>
            {isBooleanType ? (
              <div className="flex items-center gap-3">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={overrideValue === 'true'}
                    onChange={e => setOverrideValue(e.target.checked ? 'true' : 'false')}
                    className="h-4 w-4 rounded border-border accent-primary"
                  />
                  <span className="text-sm text-foreground">
                    {overrideValue === 'true' ? t('featureFlags.enabled') : t('featureFlags.disabled')}
                  </span>
                </label>
              </div>
            ) : (
              <Input
                id="ff-override-value"
                value={overrideValue}
                onChange={e => setOverrideValue(e.target.value)}
                required
              />
            )}
          </div>

          <DialogFooter>
            {flag?.tenantOverrideValue !== null && (
              <Button
                type="button"
                variant="destructive"
                onClick={handleRemoveOverride}
                disabled={!tenantId || isLoading}
              >
                {removeOverrideMutation.isPending ? t('common.loading') : t('featureFlags.removeOverride')}
              </Button>
            )}
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              disabled={!tenantId || !overrideValue || isLoading}
            >
              {setOverrideMutation.isPending ? t('common.loading') : t('featureFlags.setOverride')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
