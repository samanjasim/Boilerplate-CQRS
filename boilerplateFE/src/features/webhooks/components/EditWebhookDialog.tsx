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
import { useUpdateWebhook } from '../api';
import { EventSelector } from './EventSelector';
import type { UpdateWebhookData, WebhookEndpoint } from '@/types';

interface EditWebhookDialogProps {
  endpoint: WebhookEndpoint | null;
  onOpenChange: (open: boolean) => void;
}

export function EditWebhookDialog({ endpoint, onOpenChange }: EditWebhookDialogProps) {
  const { t } = useTranslation();
  const [form, setForm] = useState<UpdateWebhookData>({
    id: '',
    url: '',
    description: '',
    events: [],
    isActive: true,
  });

  const { mutate: updateWebhook, isPending } = useUpdateWebhook();

  // Sync form when endpoint changes
  useEffect(() => {
    if (endpoint) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setForm({
        id: endpoint.id,
        url: endpoint.url,
        description: endpoint.description ?? '',
        events: endpoint.events,
        isActive: endpoint.isActive,
      });
    }
  }, [endpoint]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    updateWebhook(form, {
      onSuccess: () => onOpenChange(false),
    });
  };

  const handleClose = () => onOpenChange(false);

  return (
    <Dialog open={!!endpoint} onOpenChange={(open) => !open && handleClose()}>
      <DialogContent className="sm:max-w-xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('webhooks.editEndpoint')}</DialogTitle>
          <DialogDescription>{t('webhooks.subtitle')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* URL */}
          <div className="space-y-2">
            <Label htmlFor="wh-edit-url">{t('webhooks.url')}</Label>
            <Input
              id="wh-edit-url"
              type="url"
              placeholder="https://hooks.example.com/webhook"
              value={form.url}
              onChange={(e) => setForm((p) => ({ ...p, url: e.target.value }))}
              required
            />
          </div>

          {/* Description */}
          <div className="space-y-2">
            <Label htmlFor="wh-edit-desc">{t('webhooks.description')}</Label>
            <Textarea
              id="wh-edit-desc"
              rows={2}
              value={form.description ?? ''}
              onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))}
            />
          </div>

          {/* Events */}
          <EventSelector
            selectedEvents={form.events}
            onChange={(events) => setForm((p) => ({ ...p, events }))}
          />

          {/* Active toggle */}
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))}
              className="accent-primary"
            />
            <span className="text-sm">{t('webhooks.active')}</span>
          </label>

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={handleClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={isPending || form.events.length === 0}>
              {isPending ? t('common.saving') : t('common.save')}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
