import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';
import type { AuditLog } from '@/types';

interface AuditTimelineHeroProps {
  rows: AuditLog[];
  totalCount: number;
  windowMs: number;
  now: number;
  truncated: boolean;
  className?: string;
}

interface Bucket {
  start: number;
  count: number;
}

function bucketRows(rows: AuditLog[], windowMs: number, now: number): Bucket[] {
  const bucketSize =
    windowMs <= 60 * 60 * 1000
      ? 60 * 1000
      : windowMs <= 24 * 60 * 60 * 1000
        ? 60 * 60 * 1000
        : 24 * 60 * 60 * 1000;
  const numBuckets = Math.max(1, Math.ceil(windowMs / bucketSize));
  const startEdge = now - numBuckets * bucketSize;
  const buckets = Array.from({ length: numBuckets }, (_, i) => ({
    start: startEdge + i * bucketSize,
    count: 0,
  }));

  for (const row of rows) {
    const timestamp = new Date(row.performedAt).getTime();
    const idx = Math.floor((timestamp - startEdge) / bucketSize);
    const bucket = buckets[idx];
    if (bucket) bucket.count += 1;
  }

  return buckets;
}

function buildSparklinePath(buckets: Bucket[], width: number, height: number): string {
  if (buckets.length === 0) return '';
  const max = Math.max(1, ...buckets.map((bucket) => bucket.count));
  const stepX = width / Math.max(1, buckets.length - 1);

  return buckets
    .map((bucket, i) => {
      const x = i * stepX;
      const y = height - (bucket.count / max) * height;
      return `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
    })
    .join(' ');
}

export function AuditTimelineHero({ rows, totalCount, windowMs, now, truncated, className }: AuditTimelineHeroProps) {
  const { t } = useTranslation();
  const buckets = useMemo(() => bucketRows(rows, windowMs, now), [rows, windowMs, now]);
  const path = useMemo(() => buildSparklinePath(buckets, 800, 60), [buckets]);
  const isEmpty = totalCount === 0;

  const windowKey =
    windowMs <= 60 * 60 * 1000
      ? 'lastHour'
      : windowMs <= 24 * 60 * 60 * 1000
        ? 'last24h'
        : windowMs <= 7 * 24 * 60 * 60 * 1000
          ? 'last7d'
          : 'last30d';

  return (
    <div className={cn('surface-glass relative overflow-hidden rounded-2xl p-6', className)}>
      <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-primary/40 to-transparent" />
      <div className="grid gap-6 md:grid-cols-[200px_1fr]">
        <div>
          <div className="gradient-text text-3xl font-semibold tabular-nums">
            {totalCount.toLocaleString()}
          </div>
          <div className="text-sm text-muted-foreground">
            {t('auditLogs.timeline.totalEvents', { count: totalCount })}
          </div>
          <div className="mt-1 text-xs text-muted-foreground">
            {t('auditLogs.timeline.windowLabel', {
              window: t(`auditLogs.timeline.windowOptions.${windowKey}`),
            })}
          </div>
        </div>

        <div className="flex flex-col justify-center">
          {isEmpty ? (
            <div className="text-sm italic text-muted-foreground">
              {t('auditLogs.timeline.noEvents')}
            </div>
          ) : (
            <svg
              viewBox="0 0 800 60"
              preserveAspectRatio="none"
              className="h-[60px] w-full"
              role="img"
              aria-label={t('auditLogs.timeline.ariaLabel', { count: totalCount })}
            >
              <defs>
                <linearGradient id="audit-spark-grad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--primary)" stopOpacity="0.4" />
                  <stop offset="100%" stopColor="var(--primary)" stopOpacity="0" />
                </linearGradient>
              </defs>
              <path d={path} fill="none" stroke="var(--primary)" strokeWidth="1.5" strokeLinejoin="round" />
              <path d={`${path} L 800 60 L 0 60 Z`} fill="url(#audit-spark-grad)" opacity="0.3" />
            </svg>
          )}

          <ul className="sr-only" aria-label="Timeline buckets">
            {buckets.map((bucket) => (
              <li key={bucket.start}>
                {new Date(bucket.start).toISOString()}: {bucket.count}
              </li>
            ))}
          </ul>
        </div>
      </div>

      {truncated && (
        <div className="mt-4 rounded-md border border-[var(--color-amber-200)] bg-[var(--color-amber-50)]/60 px-3 py-2 text-xs text-[var(--color-amber-700)] dark:border-[var(--color-amber-900)] dark:bg-[var(--color-amber-950)]/40 dark:text-[var(--color-amber-300)]">
          {t('auditLogs.timeline.truncatedBanner')}
        </div>
      )}
    </div>
  );
}
