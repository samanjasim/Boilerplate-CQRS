import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { EmptyState } from '@/components/common';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import type { StateMetric } from '@/types/workflow.types';
import { Hourglass } from 'lucide-react';

interface Props {
  states: StateMetric[];
}

export function BottleneckStatesChart({ states }: Props) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-1 text-sm font-semibold text-foreground">{t('workflow.analytics.bottleneck.title')}</h3>
        <p className="mb-3 text-xs text-muted-foreground">{t('workflow.analytics.bottleneck.subtitle')}</p>
        {states.length > 0 ? (
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={states} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis type="number" tick={{ fontSize: 11 }} unit="h" />
              <YAxis type="category" dataKey="stateName" width={120} tick={{ fontSize: 11 }} />
              <Tooltip />
              <Bar dataKey="medianDwellHours" fill="var(--primary)" />
            </BarChart>
          </ResponsiveContainer>
        ) : (
          <EmptyState icon={Hourglass} title={t('workflow.analytics.bottleneck.empty')} />
        )}
      </CardContent>
    </Card>
  );
}
