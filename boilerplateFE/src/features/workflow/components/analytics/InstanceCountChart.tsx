import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { EmptyState } from '@/components/common';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, Legend } from 'recharts';
import type { InstanceCountPoint } from '@/types/workflow.types';
import { BarChart3 } from 'lucide-react';

interface Props {
  series: InstanceCountPoint[];
}

export function InstanceCountChart({ series }: Props) {
  const { t } = useTranslation();
  const hasData = series.some((p) => p.started + p.completed + p.cancelled > 0);

  const data = series.map((p) => ({
    bucket: new Date(p.bucket).toLocaleDateString(),
    started: p.started,
    completed: p.completed,
    cancelled: p.cancelled,
  }));

  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.instanceSeries.title')}</h3>
        {hasData ? (
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={data}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis dataKey="bucket" tick={{ fontSize: 11 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
              <Tooltip />
              <Legend wrapperStyle={{ fontSize: 11 }} />
              <Bar dataKey="started"   stackId="a" fill="var(--primary)" />
              <Bar dataKey="completed" stackId="a" fill="var(--chart-2, #10b981)" />
              <Bar dataKey="cancelled" stackId="a" fill="var(--destructive)" />
            </BarChart>
          </ResponsiveContainer>
        ) : (
          <EmptyState icon={BarChart3} title={t('common.empty', 'No data')} />
        )}
      </CardContent>
    </Card>
  );
}
