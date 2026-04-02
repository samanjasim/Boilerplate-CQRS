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
import { useUpdateWebhook, useWebhookEventTypes } from '../api';
import type { UpdateWebhookData, WebhookEndpoint, WebhookEventType } from '@/types';

interface EditWebhookDialogProps {
  endpoint: WebhookEndpoint | null;
  onOpenChange: (open: boolean) => void;
}

function groupEventTypes(eventTypes: WebhookEventType[]): Record<string, WebhookEventType[]> {
  return eventTypes.reduce<Record<string, WebhookEventType[]>>((acc, et) => {
    if (!acc[et.resource]) acc[et.resource] = [];
    acc[et.resource].push(et);
    return acc;
  }, {});
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
  const { data: eventTypes = [] } = useWebhookEventTypes();
  const grouped = groupEventTypes(eventTypes);

  // Sync form when endpoint changes
  useEffect(() => {
    if (endpoint) {
      setForm({
        id: endpoint.id,
        url: endpoint.url,
        description: endpoint.description ?? '',
        events: endpoint.events,
        isActive: endpoint.isActive,
      });
    }
  }, [endpoint]);

  const toggleEvent = (eventType: string) => {
    setForm((prev) => ({
      ...prev,
      events: prev.events.includes(eventType)
        ? prev.events.filter((e) => e !== eventType)
        : [...prev.events, eventType],
    }));
  };

  const toggleGroup = (resource: string) => {
    const groupEvents = (grouped[resource] ?? []).map((e) => e.type);
    const allSelected = groupEvents.every((e) => form.events.includes(e));
    setForm((prev) => ({
      ...prev,
      events: allSelected
        ? prev.events.filter((e) => !groupEvents.includes(e))
        : [...new Set([...prev.events, ...groupEvents])],
    }));
  };

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
          <div className="space-y-2">
            <Label>{t('webhooks.selectEvents')}</Label>
            {eventTypes.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('common.loading')}</p>
            ) : (
              <div className="space-y-3 rounded-xl border border-border p-4 max-h-56 overflow-y-auto">
                {Object.entries(grouped).map(([resource, types]) => {
                  const groupEvents = types.map((t) => t.type);
                  const allSelected = groupEvents.every((e) => form.events.includes(e));
                  const someSelected = groupEvents.some((e) => form.events.includes(e));

                  return (
                    <div key={resource} className="space-y-1.5">
                      <label className="flex items-center gap-2 cursor-pointer select-none">
                        <input
                          type="checkbox"
                          checked={allSelected}
                          ref={(el) => {
                            if (el) el.indeterminate = someSelected && !allSelected;
                          }}
                          onChange={() => toggleGroup(resource)}
                          className="accent-primary h-3.5 w-3.5"
                        />
                        <span className="text-xs font-semibold text-foreground uppercase tracking-wide">
                          {resource}
                        </span>
                      </label>
                      <div className="ml-5 space-y-1">
                        {types.map((et) => (
                          <label
                            key={et.type}
                            className="flex items-center gap-2 cursor-pointer select-none"
                          >
                            <input
                              type="checkbox"
                              checked={form.events.includes(et.type)}
                              onChange={() => toggleEvent(et.type)}
                              className="accent-primary h-3.5 w-3.5"
                            />
                            <span className="text-sm text-foreground">{et.type}</span>
                            {et.description && (
                              <span className="text-xs text-muted-foreground">— {et.description}</span>
                            )}
                          </label>
                        ))}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

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
