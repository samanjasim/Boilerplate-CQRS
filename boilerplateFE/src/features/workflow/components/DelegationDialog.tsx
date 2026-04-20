import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useCreateDelegation } from '../api';

interface DelegationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DelegationDialog({ open, onOpenChange }: DelegationDialogProps) {
  const { t } = useTranslation();
  const [toUserId, setToUserId] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const { mutate: createDelegation, isPending } = useCreateDelegation();

  const handleConfirm = () => {
    if (!toUserId || !startDate || !endDate) return;
    createDelegation(
      { toUserId, startDate, endDate },
      {
        onSuccess: () => {
          setToUserId('');
          setStartDate('');
          setEndDate('');
          onOpenChange(false);
        },
      },
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('workflow.delegation.title')}</DialogTitle>
          <DialogDescription>
            {t('workflow.delegation.description', 'Delegate your pending tasks to another user for a specific period.')}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="delegation-user">{t('workflow.delegation.delegateTo')}</Label>
            <Input
              id="delegation-user"
              value={toUserId}
              onChange={(e) => setToUserId(e.target.value)}
              placeholder={t('workflow.delegation.userPlaceholder', 'Enter user ID')}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label htmlFor="delegation-start">{t('workflow.delegation.startDate')}</Label>
              <Input
                id="delegation-start"
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="delegation-end">{t('workflow.delegation.endDate')}</Label>
              <Input
                id="delegation-end"
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button
            onClick={handleConfirm}
            disabled={isPending || !toUserId || !startDate || !endDate}
          >
            {t('workflow.delegation.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
