import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import type { HeadlineMetrics } from '@/types/workflow.types';

interface Props {
  headline: HeadlineMetrics;
}

export function HeadlineStrip({ headline }: Props) {
  const { t } = useTranslation();
  const stats = [
    { label: t('workflow.analytics.headline.started'),   value: headline.totalStarted },
    { label: t('workflow.analytics.headline.completed'), value: headline.totalCompleted },
    { label: t('workflow.analytics.headline.cancelled'), value: headline.totalCancelled },
    {
      label: t('workflow.analytics.headline.avgCycleTime'),
      value: headline.avgCycleTimeHours !== null
        ? `${headline.avgCycleTimeHours.toFixed(1)}${t('workflow.analytics.hoursShort')}`
        : '—',
    },
  ];
  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
      {stats.map((s) => (
        <Card key={s.label}>
          <CardContent className="py-4">
            <p className="text-xs font-medium text-muted-foreground">{s.label}</p>
            <p className="mt-1 text-2xl font-semibold text-foreground">{s.value}</p>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
