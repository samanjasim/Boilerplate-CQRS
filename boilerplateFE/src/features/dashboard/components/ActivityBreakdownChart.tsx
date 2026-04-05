import { useTranslation } from 'react-i18next';
import {
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Tooltip,
} from 'recharts';
import { Card, CardContent } from '@/components/ui/card';
import type { TimeSeriesPoint } from '@/types/dashboard.types';

interface ActivityBreakdownChartProps {
  data: TimeSeriesPoint[];
}

const CHART_COLORS = [
  'hsl(var(--chart-1))',
  'hsl(var(--chart-2))',
  'hsl(var(--chart-3))',
  'hsl(var(--chart-4))',
  'hsl(var(--chart-5))',
];

export function ActivityBreakdownChart({ data }: ActivityBreakdownChartProps) {
  const { t } = useTranslation();

  const topData = data.slice(0, 5);

  return (
    <Card>
      <CardContent className="pt-6">
        <h3 className="text-base font-semibold text-foreground tracking-tight mb-4">
          {t('dashboard.activityBreakdown')}
        </h3>
        {topData.length === 0 ? (
          <div className="flex items-center justify-center h-[200px] text-sm text-muted-foreground">
            {t('dashboard.noData')}
          </div>
        ) : (
          <div className="flex items-center gap-6">
            <div className="flex-shrink-0">
              <ResponsiveContainer width={200} height={200}>
                <PieChart>
                  <Pie
                    data={topData}
                    dataKey="value"
                    nameKey="date"
                    innerRadius={60}
                    outerRadius={90}
                    paddingAngle={2}
                  >
                    {topData.map((_, index) => (
                      <Cell
                        key={`cell-${index}`}
                        fill={CHART_COLORS[index % CHART_COLORS.length]}
                      />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-xs font-medium text-muted-foreground mb-2">
                {t('dashboard.topEntities')}
              </p>
              <ul className="space-y-2">
                {topData.map((item, index) => (
                  <li key={item.date} className="flex items-center gap-2">
                    <span
                      className="h-2.5 w-2.5 rounded-full flex-shrink-0"
                      style={{ backgroundColor: CHART_COLORS[index % CHART_COLORS.length] }}
                    />
                    <span className="text-sm text-foreground truncate flex-1">{item.date}</span>
                    <span className="text-sm font-medium text-foreground tabular-nums">
                      {new Intl.NumberFormat('en-US').format(item.value)}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
