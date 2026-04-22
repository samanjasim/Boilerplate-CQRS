import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HardDrive } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { UserAvatar } from '@/components/common';
import { useAuthStore } from '@/stores/auth.store';
import { useStorageSummary } from '../api/files.queries';
import { formatFileSize } from '@/utils';

export function StorageSummaryPanel() {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const user = useAuthStore(s => s.user);
  const isPlatformAdmin = !user?.tenantId;
  const [allTenants, setAllTenants] = useState(false);

  const { data, isLoading } = useStorageSummary(allTenants && isPlatformAdmin);

  const maxBytes = data?.byCategory.reduce((m, c) => Math.max(m, c.bytes), 1) ?? 1;

  return (
    <>
      <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
        <HardDrive className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
        {t('files.storageSummary.trigger')}
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('files.storageSummary.title')}</DialogTitle>
          </DialogHeader>

          {isLoading ? (
            <div className="flex justify-center py-8"><Spinner /></div>
          ) : data ? (
            <div className="space-y-5 text-sm">
              {/* Total */}
              <div className="flex items-center justify-between rounded-lg bg-muted px-4 py-3">
                <span className="font-medium">{t('files.storageSummary.total')}</span>
                <span className="text-lg font-semibold">{formatFileSize(data.totalBytes)}</span>
              </div>

              {/* By category */}
              <div className="space-y-2">
                <p className="font-medium text-muted-foreground">{t('files.storageSummary.byCategory')}</p>
                {data.byCategory.map(c => (
                  <div key={c.category} className="space-y-1">
                    <div className="flex justify-between text-xs">
                      <span>{c.category}</span>
                      <span className="text-muted-foreground">{formatFileSize(c.bytes)} · {c.fileCount}</span>
                    </div>
                    <div className="h-1.5 rounded-full bg-muted">
                      <div
                        className="h-full rounded-full bg-primary transition-all"
                        style={{ width: `${(c.bytes / maxBytes) * 100}%` }}
                      />
                    </div>
                  </div>
                ))}
              </div>

              {/* Top uploaders */}
              {data.topUploaders.length > 0 && (
                <div className="space-y-2">
                  <p className="font-medium text-muted-foreground">{t('files.storageSummary.topUploaders')}</p>
                  <ul className="divide-y rounded-md border">
                    {data.topUploaders.map(u => (
                      <li key={u.userId} className="flex items-center justify-between px-3 py-2 gap-2">
                        <div className="flex items-center gap-2">
                          <UserAvatar size="xs" firstName={u.userName?.split(' ')[0]} lastName={u.userName?.split(' ')[1]} />
                          <span className="truncate max-w-[160px]">{u.userName ?? u.userId}</span>
                        </div>
                        <span className="text-muted-foreground shrink-0">{formatFileSize(u.bytes)}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Platform admin toggle */}
              {isPlatformAdmin && (
                <label className="flex items-center gap-2 text-xs">
                  <input
                    type="checkbox"
                    checked={allTenants}
                    onChange={e => setAllTenants(e.target.checked)}
                    className="h-4 w-4"
                  />
                  {t('files.storageSummary.allTenants')}
                </label>
              )}
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
