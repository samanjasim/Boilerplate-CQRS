import { useTranslation } from 'react-i18next';
import { format, parseISO } from 'date-fns';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from 'recharts';
import { Card, CardContent } from '@/components/ui/card';
import type { TimeSeriesPoint } from '@/types/dashboard.types';

interface LoginActivityChartProps {
  data: TimeSeriesPoint[];
}

function formatShortDate(dateStr: string) {
  try {
    return format(parseISO(dateStr), 'MMM d');
  } catch {
    return dateStr;
  }
}

function formatFullDate(dateStr: string) {
  try {
    return format(parseISO(dateStr), 'PPP');
  } catch {
    return dateStr;
  }
}

export function LoginActivityChart({ data }: LoginActivityChartProps) {
  const { t } = useTranslation();

  return (
    <Card>
      <CardContent className="pt-6">
        <h3 className="text-base font-semibold text-foreground tracking-tight mb-4">
          {t('dashboard.loginActivity')}
        </h3>
        {data.length === 0 ? (
          <div className="flex items-center justify-center h-[250px] text-sm text-muted-foreground">
            {t('dashboard.noData')}
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={250}>
            <BarChart data={data}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis
                dataKey="date"
                tickFormatter={formatShortDate}
                tick={{ fontSize: 12 }}
              />
              <YAxis tick={{ fontSize: 12 }} />
              <Tooltip labelFormatter={(label) => formatFullDate(String(label))} />
              <Bar dataKey="value" fill="hsl(var(--chart-2))" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}
