import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { SubjectPicker } from './SubjectPicker';
import { useTransferResourceOwnership } from '@/features/access/api/access.queries';
import type { ResourceType } from '@/features/access/types';

type Props = {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  resourceType: ResourceType;
  resourceId: string;
  resourceName: string;
  currentOwnerId: string;
};

export function OwnershipTransferDialog({
  open,
  onOpenChange,
  resourceType,
  resourceId,
  resourceName,
  currentOwnerId,
}: Props) {
  const { t } = useTranslation();
  const [newOwnerId, setNewOwnerId] = useState<string | null>(null);
  const transfer = useTransferResourceOwnership(resourceType, resourceId);

  const handleConfirm = async () => {
    if (!newOwnerId) return;
    await transfer.mutateAsync(newOwnerId);
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('access.transferOwnership.title', { name: resourceName })}</DialogTitle>
          <DialogDescription>{t('access.transferOwnership.description')}</DialogDescription>
        </DialogHeader>
        <SubjectPicker
          mode="user-only"
          excludeIds={[currentOwnerId]}
          onSelect={s => setNewOwnerId(s.id)}
        />
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t('common.cancel')}
          </Button>
          <Button onClick={handleConfirm} disabled={!newOwnerId || transfer.isPending}>
            {t('access.transferOwnership.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
