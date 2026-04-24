import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { EmptyState } from '@/components/common';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, Legend } from 'recharts';
import type { ActionRateMetric } from '@/types/workflow.types';
import { Vote } from 'lucide-react';

interface Props {
  rates: ActionRateMetric[];
}

// --primary / --destructive are HSL triples (need hsl(...)); --chart-N tokens
// aren't defined in styles/index.css, so use hex fallbacks directly.
const palette = ['var(--color-primary)', 'hsl(var(--destructive))', '#10b981', '#f59e0b'];

export function ActionRatesChart({ rates }: Props) {
  const { t } = useTranslation();

  const { data, actions } = useMemo(() => {
    const stateMap = new Map<string, Record<string, number | string>>();
    const actionSet = new Set<string>();
    for (const r of rates) {
      actionSet.add(r.action);
      const row = stateMap.get(r.stateName) ?? { state: r.stateName };
      row[r.action] = r.count;
      stateMap.set(r.stateName, row);
    }
    return { data: Array.from(stateMap.values()), actions: Array.from(actionSet) };
  }, [rates]);

  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.actionRates.title')}</h3>
        {rates.length > 0 ? (
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={data}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis dataKey="state" tick={{ fontSize: 11 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
              <Tooltip />
              <Legend wrapperStyle={{ fontSize: 11 }} />
              {actions.map((a, i) => (
                <Bar key={a} dataKey={a} fill={palette[i % palette.length]} />
              ))}
            </BarChart>
          </ResponsiveContainer>
        ) : (
          <EmptyState icon={Vote} title={t('workflow.analytics.actionRates.empty')} />
        )}
      </CardContent>
    </Card>
  );
}
