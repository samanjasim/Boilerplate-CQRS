import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Copy, Check, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/common';
import type { CreateApiKeyResponse } from '../api';

interface ApiKeySecretDisplayProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  response: CreateApiKeyResponse;
}

export function ApiKeySecretDisplay({ open, onOpenChange, response }: ApiKeySecretDisplayProps) {
  const { t } = useTranslation();
  // Capture secret on mount so a parent re-render that drops `response.fullKey`
  // can't strip the value before the user copies. The `key={createdKey.id}` on the
  // parent guarantees a fresh component instance per created key.
  const [secret] = useState(response.fullKey);
  const [copied, setCopied] = useState(false);
  const [confirmingClose, setConfirmingClose] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(secret);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 2000);
  };

  const handleAttemptClose = () => {
    if (!copied) {
      setConfirmingClose(true);
      return;
    }
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={(next) => (next ? onOpenChange(true) : handleAttemptClose())}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('apiKeys.reveal.title')}</DialogTitle>
          <DialogDescription>{response.name}</DialogDescription>
        </DialogHeader>

        <div role="alert" aria-live="assertive" className="sr-only">
          {t('apiKeys.reveal.ariaAnnouncement')}
        </div>

        <div className="space-y-4">
          <div className="flex items-start gap-3 rounded-lg border border-[var(--color-amber-300)] bg-[var(--color-amber-50)]/60 p-4 dark:border-[var(--color-amber-700)] dark:bg-[var(--color-amber-950)]/40">
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-[var(--color-amber-600)] dark:text-[var(--color-amber-400)]" />
            <p className="text-sm text-[var(--color-amber-800)] dark:text-[var(--color-amber-200)]">
              {t('apiKeys.reveal.warning')}
            </p>
          </div>

          <div className="rounded-lg border bg-card p-4 font-mono text-sm break-all gradient-text" dir="ltr">
            {secret}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleAttemptClose}>
            {t('apiKeys.reveal.doneButton')}
          </Button>
          <Button onClick={handleCopy}>
            {copied ? (
              <>
                <Check className="me-1 h-4 w-4" />
                {t('apiKeys.reveal.copiedConfirmation')}
              </>
            ) : (
              <>
                <Copy className="me-1 h-4 w-4" />
                {t('apiKeys.reveal.copyButton')}
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>

      <ConfirmDialog
        isOpen={confirmingClose}
        onClose={() => setConfirmingClose(false)}
        title={t('apiKeys.reveal.closeConfirmTitle')}
        description={t('apiKeys.reveal.closeConfirmDescription')}
        confirmLabel={t('common.close')}
        onConfirm={() => onOpenChange(false)}
        variant="primary"
      />
    </Dialog>
  );
}
