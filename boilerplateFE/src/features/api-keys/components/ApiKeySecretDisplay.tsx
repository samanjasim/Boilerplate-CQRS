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
import type { CreateApiKeyResponse } from '../api';

interface ApiKeySecretDisplayProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  response: CreateApiKeyResponse;
}

export function ApiKeySecretDisplay({ open, onOpenChange, response }: ApiKeySecretDisplayProps) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(response.fullKey);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('apiKeys.secretTitle')}</DialogTitle>
          <DialogDescription>{t('apiKeys.secretDescription')}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="flex items-start gap-2 rounded-xl border border-yellow-500/30 bg-yellow-500/5 p-3">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-yellow-600" />
            <p className="text-sm text-yellow-700 dark:text-yellow-400">
              {t('apiKeys.secretWarning')}
            </p>
          </div>

          <div className="space-y-2">
            <p className="text-sm font-medium text-foreground">{response.name}</p>
            <div className="flex items-center gap-2">
              <code className="flex-1 rounded-xl bg-secondary p-3 text-xs break-all font-mono text-foreground">
                {response.fullKey}
              </code>
              <Button variant="outline" size="icon" onClick={handleCopy}>
                {copied ? (
                  <Check className="h-4 w-4 text-green-600" />
                ) : (
                  <Copy className="h-4 w-4" />
                )}
              </Button>
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button onClick={() => onOpenChange(false)}>
            {t('apiKeys.secretDone')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
