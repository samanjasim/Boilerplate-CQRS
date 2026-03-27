import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle } from 'lucide-react';
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
import { useEmergencyRevokeApiKey } from '../api';
import type { ApiKeyDto } from '../api';

interface EmergencyRevokeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  apiKey: ApiKeyDto | null;
}

export function EmergencyRevokeDialog({ open, onOpenChange, apiKey }: EmergencyRevokeDialogProps) {
  const { t } = useTranslation();
  const emergencyRevoke = useEmergencyRevokeApiKey();
  const [confirmName, setConfirmName] = useState('');
  const [reason, setReason] = useState('');

  const isConfirmed = confirmName === apiKey?.name;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!apiKey || !isConfirmed) return;
    await emergencyRevoke.mutateAsync({ id: apiKey.id, reason: reason || undefined });
    setConfirmName('');
    setReason('');
    onOpenChange(false);
  };

  const handleClose = () => {
    setConfirmName('');
    setReason('');
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('apiKeys.emergencyRevokeTitle')}</DialogTitle>
          <DialogDescription>{t('apiKeys.emergencyRevokeDescription')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="flex items-start gap-2 rounded-xl border border-destructive/30 bg-destructive/5 p-3">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
            <p className="text-sm text-destructive">
              {t('apiKeys.emergencyRevokeWarning', { tenant: apiKey?.tenantName ?? t('common.unknown') })}
            </p>
          </div>

          <div className="space-y-2">
            <Label>{t('apiKeys.emergencyRevokeConfirmLabel', { name: apiKey?.name })}</Label>
            <Input
              value={confirmName}
              onChange={e => setConfirmName(e.target.value)}
              placeholder={apiKey?.name ?? ''}
            />
          </div>

          <div className="space-y-2">
            <Label>{t('apiKeys.emergencyRevokeReason')}</Label>
            <Textarea
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder={t('apiKeys.emergencyRevokeReasonPlaceholder')}
              rows={2}
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={handleClose}>
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              variant="destructive"
              disabled={!isConfirmed || emergencyRevoke.isPending}
            >
              {emergencyRevoke.isPending ? t('common.loading') : t('apiKeys.emergencyRevokeConfirm')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
