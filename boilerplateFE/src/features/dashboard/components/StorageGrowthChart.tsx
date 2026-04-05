import { useTranslation } from 'react-i18next';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from 'recharts';
import { Card, CardContent } from '@/components/ui/card';
import type { TimeSeriesPoint } from '@/types/dashboard.types';
import { formatShortDate, formatFullDate, formatBytes } from '../utils/format';

interface StorageGrowthChartProps {
  data: TimeSeriesPoint[];
}

export function StorageGrowthChart({ data }: StorageGrowthChartProps) {
  const { t } = useTranslation();

  return (
    <Card>
      <CardContent className="pt-6">
        <h3 className="text-base font-semibold text-foreground tracking-tight mb-4">
          {t('dashboard.storageGrowth')}
        </h3>
        {data.length === 0 ? (
          <div className="flex items-center justify-center h-[250px] text-sm text-muted-foreground">
            {t('dashboard.noData')}
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={250}>
            <AreaChart data={data}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis
                dataKey="date"
                tickFormatter={formatShortDate}
                tick={{ fontSize: 12 }}
              />
              <YAxis tickFormatter={formatBytes} tick={{ fontSize: 12 }} />
              <Tooltip
                labelFormatter={(label) => formatFullDate(String(label))}
                formatter={(value) => [formatBytes(Number(value)), t('dashboard.storage')]}
              />
              <defs>
                <linearGradient id="storageGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="hsl(var(--chart-3))" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="hsl(var(--chart-3))" stopOpacity={0} />
                </linearGradient>
              </defs>
              <Area
                type="monotone"
                dataKey="value"
                stroke="hsl(var(--chart-3))"
                fill="url(#storageGradient)"
              />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}
