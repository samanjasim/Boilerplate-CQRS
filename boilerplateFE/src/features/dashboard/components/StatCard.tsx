import { TrendingUp, TrendingDown, Minus } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { formatBytes } from '../utils/format';

function formatValue(value: number, format: 'number' | 'bytes' | 'currency'): string {
  if (format === 'number') {
    return new Intl.NumberFormat().format(value);
  }
  if (format === 'currency') {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' }).format(value);
  }
  return formatBytes(value);
}

interface StatCardProps {
  icon: LucideIcon;
  label: string;
  value: number;
  format: 'number' | 'bytes' | 'currency';
  trend: number | null;
  period: string;
}

export function StatCard({ icon: Icon, label, value, format, trend, period }: StatCardProps) {
  const { t } = useTranslation();

  const isPositive = trend !== null && trend > 0;
  const isNegative = trend !== null && trend < 0;

  return (
    <Card className="hover-lift">
      <CardContent className="py-5">
        <div className="flex items-center gap-3 mb-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-xl [background:var(--active-bg)]">
            <Icon className="h-5 w-5 [color:var(--active-text)]" />
          </div>
          <p className="text-sm text-muted-foreground">{label}</p>
        </div>
        <p className="text-2xl font-bold text-foreground mb-2">{formatValue(value, format)}</p>
        <div className="flex items-center gap-1.5">
          {trend === null || trend === 0 ? (
            <>
              <Minus className="h-3.5 w-3.5 text-muted-foreground" />
              <span className="text-xs text-muted-foreground">{t('dashboard.trend')}</span>
            </>
          ) : isPositive ? (
            <>
              <TrendingUp className="h-3.5 w-3.5 text-green-500" />
              <span className="text-xs text-green-600">+{trend?.toFixed(1)}%</span>
              <span className="text-xs text-muted-foreground">{t('dashboard.vsPrevious', { period: t(`dashboard.${period}`) })}</span>
            </>
          ) : isNegative ? (
            <>
              <TrendingDown className="h-3.5 w-3.5 text-red-500" />
              <span className="text-xs text-red-600">{trend?.toFixed(1)}%</span>
              <span className="text-xs text-muted-foreground">{t('dashboard.vsPrevious', { period: t(`dashboard.${period}`) })}</span>
            </>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}
