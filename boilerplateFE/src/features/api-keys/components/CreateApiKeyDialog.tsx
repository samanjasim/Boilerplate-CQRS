import { useState } from 'react';
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
import { useCreateApiKey } from '../api';
import type { CreateApiKeyResponse } from '../api';

const AVAILABLE_SCOPES = [
  'Users.View',
  'Users.Create',
  'Users.Update',
  'Users.Delete',
  'Roles.View',
  'Files.View',
  'Files.Upload',
  'Files.Delete',
  'System.ExportData',
  'System.ViewAuditLogs',
];

interface CreateApiKeyDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (response: CreateApiKeyResponse) => void;
  isPlatform?: boolean;
}

export function CreateApiKeyDialog({ open, onOpenChange, onCreated, isPlatform }: CreateApiKeyDialogProps) {
  const { t } = useTranslation();
  const createMutation = useCreateApiKey();
  const [name, setName] = useState('');
  const [scopes, setScopes] = useState<string[]>([]);
  const [expiresAt, setExpiresAt] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const result = await createMutation.mutateAsync({
      name,
      scopes,
      expiresAt: expiresAt || null,
      isPlatformKey: isPlatform ?? false,
    });
    setName('');
    setScopes([]);
    setExpiresAt('');
    onCreated(result);
  };

  const toggleScope = (scope: string) => {
    setScopes(prev =>
      prev.includes(scope)
        ? prev.filter(s => s !== scope)
        : [...prev, scope]
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('apiKeys.createTitle')}</DialogTitle>
          <DialogDescription>{t('apiKeys.createDescription')}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">{t('apiKeys.name')}</Label>
            <Input
              id="name"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder={t('apiKeys.namePlaceholder')}
              required
              maxLength={200}
            />
          </div>

          <div className="space-y-2">
            <Label>{t('apiKeys.scopes')}</Label>
            <div className="grid grid-cols-2 gap-2 rounded-xl border border-border bg-secondary/30 p-3 max-h-48 overflow-y-auto">
              {AVAILABLE_SCOPES.map(scope => (
                <label key={scope} className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={scopes.includes(scope)}
                    onChange={() => toggleScope(scope)}
                    className="h-4 w-4 rounded border-border accent-primary"
                  />
                  <span className="text-xs text-foreground">{scope}</span>
                </label>
              ))}
            </div>
            {scopes.length === 0 && (
              <p className="text-xs text-destructive">{t('apiKeys.scopesRequired')}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="expiresAt">{t('apiKeys.expires')}</Label>
            <Input
              id="expiresAt"
              type="datetime-local"
              value={expiresAt}
              onChange={e => setExpiresAt(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">{t('apiKeys.expiresHint')}</p>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              disabled={!name || scopes.length === 0 || createMutation.isPending}
            >
              {createMutation.isPending ? t('common.loading') : t('apiKeys.create')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
