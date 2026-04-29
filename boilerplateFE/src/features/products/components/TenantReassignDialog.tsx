import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useTenants } from '@/features/tenants/api';
import { useUpdateProduct } from '../api';
import type { Product } from '@/types';

interface TenantReassignDialogProps {
  isOpen: boolean;
  onClose: () => void;
  product: Product;
}

export function TenantReassignDialog({ isOpen, onClose, product }: TenantReassignDialogProps) {
  const { t } = useTranslation();
  const [selectedTenantId, setSelectedTenantId] = useState(product.tenantId ?? '');
  const { data: tenantsData } = useTenants(isOpen ? { pageSize: 100 } : undefined);
  const updateProduct = useUpdateProduct();

  const tenants = tenantsData?.data ?? [];
  const selectedTenant = useMemo(
    () => tenants.find((tenant: { id: string; name: string }) => tenant.id === selectedTenantId),
    [selectedTenantId, tenants],
  );
  const oldTenantName = product.tenantName ?? t('common.none', 'None');
  const newTenantName = selectedTenant?.name ?? oldTenantName;
  const hasChanged = !!selectedTenantId && selectedTenantId !== (product.tenantId ?? '');

  useEffect(() => {
    if (isOpen) setSelectedTenantId(product.tenantId ?? '');
  }, [isOpen, product.tenantId]);

  async function handleMove() {
    if (!hasChanged) return;

    await updateProduct.mutateAsync({
      id: product.id,
      name: product.name,
      description: product.description,
      price: product.price,
      currency: product.currency,
      tenantId: selectedTenantId,
    });
    onClose();
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('products.detail.reassignTitle')}</DialogTitle>
          <DialogDescription>
            {t('products.detail.reassignDescription', {
              productName: product.name,
              newTenant: newTenantName,
              oldTenant: oldTenantName,
            })}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          <Label>{t('products.assignedTenant')}</Label>
          <Select value={selectedTenantId} onValueChange={setSelectedTenantId}>
            <SelectTrigger>
              <SelectValue placeholder={t('products.chooseTenant')} />
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

        <DialogFooter>
          <Button type="button" variant="ghost" onClick={onClose} disabled={updateProduct.isPending}>
            {t('products.detail.reassignCancel')}
          </Button>
          <Button type="button" onClick={handleMove} disabled={!hasChanged || updateProduct.isPending}>
            {updateProduct.isPending ? t('common.loading') : t('products.detail.reassignConfirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
