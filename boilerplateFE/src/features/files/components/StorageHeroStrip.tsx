import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { useFeatureFlag } from '@/hooks';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/stores/auth.store';
import { formatFileSize } from '@/utils';
import { useStorageSummary } from '../api/files.queries';

const MAX_BARS = 4;

export function StorageHeroStrip() {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const isPlatformAdmin = !user?.tenantId;
  const [allTenants, setAllTenants] = useState(false);
  const inCrossTenantView = isPlatformAdmin && allTenants;
  const { data, isLoading } = useStorageSummary(inCrossTenantView);
  const quotaFlag = useFeatureFlag('files.max_storage_mb');
  const quotaMb = quotaFlag.value ? Number.parseInt(quotaFlag.value, 10) : null;
  const quotaBytes = quotaMb && !Number.isNaN(quotaMb) ? quotaMb * 1024 * 1024 : null;
  const showQuota = !inCrossTenantView && quotaBytes != null && quotaBytes > 0;

  const bars = useMemo(() => {
    if (!data) return [];
    const sorted = [...data.byCategory].sort((a, b) => b.bytes - a.bytes);
    if (sorted.length <= MAX_BARS) return sorted;

    const head = sorted.slice(0, MAX_BARS - 1);
    const tail = sorted.slice(MAX_BARS - 1);
    const other = tail.reduce(
      (acc, category) => ({
        category: t('files.storageHero.other'),
        bytes: acc.bytes + category.bytes,
        fileCount: acc.fileCount + category.fileCount,
      }),
      { category: t('files.storageHero.other'), bytes: 0, fileCount: 0 }
    );
    return [...head, other];
  }, [data, t]);

  const maxBarBytes = bars.reduce((max, category) => Math.max(max, category.bytes), 1);
  const usagePct =
    showQuota && data && quotaBytes
      ? Math.min(100, (data.totalBytes / quotaBytes) * 100)
      : null;

  return (
    <div className="mb-6 grid gap-4 lg:grid-cols-[minmax(220px,0.75fr)_minmax(0,1.5fr)]">
      <div className="space-y-3">
        <MetricCard
          label={t('files.storageHero.total')}
          value={isLoading || !data ? '-' : formatFileSize(data.totalBytes)}
          secondary={
            showQuota && quotaBytes
              ? t('files.storageHero.ofQuota', { quota: formatFileSize(quotaBytes) })
              : undefined
          }
          emphasis={!isLoading && !!data}
        />
        {usagePct != null && (
          <div className="h-1.5 overflow-hidden rounded-full bg-muted">
            <div
              className={cn(
                'h-full rounded-full transition-all',
                usagePct > 90 ? 'bg-destructive' : 'bg-primary'
              )}
              style={{ width: `${usagePct}%` }}
            />
          </div>
        )}
      </div>

      <Card variant="glass">
        <CardContent className="py-5">
          <div className="grid items-start gap-6 md:grid-cols-[minmax(0,1fr)_auto]">
            <div className="space-y-2">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('files.storageHero.byCategory')}
              </div>
              {isLoading || !data ? (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Spinner size="sm" /> ...
                </div>
              ) : (
                <ul className="space-y-1.5">
                  {bars.map((category, index) => (
                    <li key={`${category.category}-${index}`} className="space-y-1">
                      <div className="flex justify-between gap-3 text-xs">
                        <span className="truncate text-muted-foreground">
                          {category.category}
                        </span>
                        <span className="shrink-0 tabular-nums text-muted-foreground">
                          {formatFileSize(category.bytes)} / {category.fileCount}
                        </span>
                      </div>
                      <div className="h-1.5 overflow-hidden rounded-full bg-muted">
                        <div
                          className="h-full rounded-full bg-primary transition-all"
                          style={{ width: `${(category.bytes / maxBarBytes) * 100}%` }}
                        />
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            {isPlatformAdmin && (
              <div className="md:justify-self-end">
                <label className="inline-flex cursor-pointer items-center gap-2 text-xs">
                  <input
                    type="checkbox"
                    checked={allTenants}
                    onChange={(event) => setAllTenants(event.target.checked)}
                    className="h-3.5 w-3.5"
                  />
                  {t('files.storageHero.allTenants')}
                </label>
              </div>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
