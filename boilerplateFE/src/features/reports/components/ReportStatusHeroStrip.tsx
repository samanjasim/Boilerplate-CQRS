import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { Spinner } from '@/components/ui/spinner';
import { useReportStatusCounts } from '../api/reports.queries';

function CountSkeleton() {
  return (
    <span
      aria-hidden="true"
      className="block h-7 w-12 animate-pulse rounded bg-muted"
    />
  );
}

export function ReportStatusHeroStrip() {
  const { t } = useTranslation();
  const { data, isLoading } = useReportStatusCounts();

  if (isLoading || !data) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-busy={isLoading}>
        <MetricCard
          label={t('reports.hero.active')}
          eyebrow={t('reports.hero.activeEyebrow')}
          value={<CountSkeleton />}
          tone="active"
        />
        <MetricCard
          label={t('reports.hero.completed')}
          eyebrow={t('reports.hero.completedEyebrow')}
          value={<CountSkeleton />}
        />
      </div>
    );
  }

  const active = data.pending + data.processing;
  const showFailed = data.failed > 0;
  const isProcessing = data.processing > 0;

  return (
    <div className={`grid gap-4 sm:grid-cols-2 ${showFailed ? 'lg:grid-cols-3' : ''}`}>
      <MetricCard
        label={t('reports.hero.active')}
        eyebrow={t('reports.hero.activeEyebrow')}
        value={active}
        emphasis={active > 0}
        tone="active"
        glyph={isProcessing ? <Spinner size="sm" className="h-4 w-4" /> : undefined}
      />
      <MetricCard
        label={t('reports.hero.completed')}
        eyebrow={t('reports.hero.completedEyebrow')}
        value={data.completed}
      />
      {showFailed && (
        <MetricCard
          label={t('reports.hero.failed')}
          eyebrow={t('reports.hero.failedEyebrow')}
          value={data.failed}
          tone="destructive"
        />
      )}
    </div>
  );
}
