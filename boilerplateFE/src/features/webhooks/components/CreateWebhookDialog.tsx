import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Copy, CheckCircle2, AlertTriangle } from 'lucide-react';
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
import { useCreateWebhook } from '../api';
import { EventSelector } from './EventSelector';
import type { CreateWebhookData, CreateWebhookResponse } from '@/types';

interface CreateWebhookDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const DEFAULT_FORM: CreateWebhookData = {
  url: '',
  description: '',
  events: [],
  isActive: true,
};

export function CreateWebhookDialog({ open, onOpenChange }: CreateWebhookDialogProps) {
  const { t } = useTranslation();
  const [form, setForm] = useState<CreateWebhookData>(DEFAULT_FORM);
  const [createdSecret, setCreatedSecret] = useState<CreateWebhookResponse | null>(null);
  const [copied, setCopied] = useState(false);
  const { mutate: createWebhook, isPending } = useCreateWebhook();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createWebhook(form, {
      onSuccess: (response) => {
        setCreatedSecret(response);
      },
    });
  };

  const handleCopy = async () => {
    if (!createdSecret?.secret) return;
    await navigator.clipboard.writeText(createdSecret.secret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleClose = () => {
    setForm(DEFAULT_FORM);
    setCreatedSecret(null);
    setCopied(false);
    onOpenChange(false);
  };

  // After creation — show secret
  if (createdSecret) {
    return (
      <Dialog open={open} onOpenChange={handleClose}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('webhooks.secret')}</DialogTitle>
            <DialogDescription>{t('webhooks.secretDesc')}</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {/* Warning banner */}
            <div className="flex items-start gap-3 rounded-xl border border-warning/40 bg-warning/10 px-4 py-3">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-warning" />
              <p className="text-sm text-warning">{t('webhooks.secretOnceWarning')}</p>
            </div>

            {/* Secret value */}
            <div className="space-y-2">
              <Label>{t('webhooks.secret')}</Label>
              <div className="flex gap-2">
                <code className="flex-1 rounded-xl border border-border bg-secondary px-3 py-2 text-xs font-mono break-all text-foreground">
                  {createdSecret.secret}
                </code>
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  onClick={handleCopy}
                  className="shrink-0"
                >
                  {copied ? (
                    <CheckCircle2 className="h-4 w-4 text-success" />
                  ) : (
                    <Copy className="h-4 w-4" />
                  )}
                </Button>
              </div>
            </div>

            <div className="flex justify-end">
              <Button onClick={handleClose}>{t('common.close')}</Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    );
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('webhooks.addEndpoint')}</DialogTitle>
          <DialogDescription>{t('webhooks.subtitle')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* URL */}
          <div className="space-y-2">
            <Label htmlFor="wh-url">{t('webhooks.url')}</Label>
            <Input
              id="wh-url"
              type="url"
              placeholder="https://hooks.example.com/webhook"
              value={form.url}
              onChange={(e) => setForm((p) => ({ ...p, url: e.target.value }))}
              required
            />
          </div>

          {/* Description */}
          <div className="space-y-2">
            <Label htmlFor="wh-desc">{t('webhooks.description')}</Label>
            <Textarea
              id="wh-desc"
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
              {isPending ? t('common.creating') : t('common.create')}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
