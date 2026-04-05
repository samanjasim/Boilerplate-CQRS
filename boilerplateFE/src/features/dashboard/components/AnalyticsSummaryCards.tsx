import { Users, HardDrive, KeyRound, Webhook, FileText, ArrowLeftRight, DollarSign, Building } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { DashboardAnalytics } from '@/types/dashboard.types';
import { StatCard } from './StatCard';

interface AnalyticsSummaryCardsProps {
  analytics: DashboardAnalytics;
  period: string;
}

const cardConfigs = [
  { section: 'users', label: 'dashboard.users', icon: Users, format: 'number' as const },
  { section: 'storage', label: 'dashboard.storage', icon: HardDrive, format: 'bytes' as const },
  { section: 'apiKeys', label: 'dashboard.apiKeys', icon: KeyRound, format: 'number' as const },
  { section: 'webhooks', label: 'dashboard.webhooks', icon: Webhook, format: 'number' as const },
  { section: 'reports', label: 'dashboard.reports', icon: FileText, format: 'number' as const },
  { section: 'imports', label: 'dashboard.imports', icon: ArrowLeftRight, format: 'number' as const },
  { section: 'revenue', label: 'dashboard.revenue', icon: DollarSign, format: 'currency' as const },
  { section: 'tenants', label: 'dashboard.tenants', icon: Building, format: 'number' as const },
];

export function AnalyticsSummaryCards({ analytics, period }: AnalyticsSummaryCardsProps) {
  const { t } = useTranslation();

  const enabledCards = cardConfigs.filter((cfg) =>
    analytics.enabledSections.includes(cfg.section),
  );

  if (enabledCards.length === 0) {
    return null;
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      {enabledCards.map(({ section, label, icon, format }) => {
        const metric = analytics.summary[section];
        return (
          <StatCard
            key={section}
            icon={icon}
            label={t(label)}
            value={metric?.current ?? 0}
            format={format}
            trend={metric?.trend ?? null}
            period={period}
          />
        );
      })}
    </div>
  );
}
