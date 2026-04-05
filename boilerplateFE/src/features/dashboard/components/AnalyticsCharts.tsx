import type { DashboardAnalytics } from '@/types/dashboard.types';
import { UserGrowthChart } from './UserGrowthChart';
import { LoginActivityChart } from './LoginActivityChart';
import { ActivityBreakdownChart } from './ActivityBreakdownChart';
import { StorageGrowthChart } from './StorageGrowthChart';
import { TenantGrowthChart } from './TenantGrowthChart';

interface AnalyticsChartsProps {
  analytics: DashboardAnalytics;
}

const chartConfigs = [
  { section: 'users', key: 'userGrowth', Component: UserGrowthChart },
  { section: 'loginActivity', key: 'loginActivity', Component: LoginActivityChart },
  { section: 'activityBreakdown', key: 'activityBreakdown', Component: ActivityBreakdownChart },
  { section: 'storage', key: 'storageGrowth', Component: StorageGrowthChart },
  { section: 'tenants', key: 'tenantGrowth', Component: TenantGrowthChart },
] as const;

export function AnalyticsCharts({ analytics }: AnalyticsChartsProps) {
  const enabledCharts = chartConfigs.filter((cfg) =>
    analytics.enabledSections.includes(cfg.section),
  );

  if (enabledCharts.length === 0) {
    return null;
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      {enabledCharts.map(({ key, Component }) => {
        const data = analytics.charts[key] ?? [];
        return <Component key={key} data={data} />;
      })}
    </div>
  );
}
