import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import type { IntegrationConfigDto } from '@/types/communication.types';

interface IntegrationsStatusHeroProps {
  configs: IntegrationConfigDto[];
}

export function IntegrationsStatusHero({ configs }: IntegrationsStatusHeroProps) {
  const { t } = useTranslation();

  const active = configs.filter((c) => c.status === 'Active').length;
  const configured = configs.filter((c) => c.status === 'Inactive').length;
  const errored = configs.filter((c) => c.status === 'Error').length;

  if (active + configured + errored === 0) {
    return null;
  }

  const showActive = active > 0;
  const showConfigured = configured > 0;
  const showErrored = errored > 0;
  const visibleCount = [showActive, showConfigured, showErrored].filter(Boolean).length;

  return (
    <div
      className={cn(
        'mb-6 grid gap-4',
        visibleCount === 1 && 'sm:grid-cols-1',
        visibleCount === 2 && 'sm:grid-cols-2',
        visibleCount === 3 && 'sm:grid-cols-2 lg:grid-cols-3',
      )}
    >
      {showActive && (
        <MetricCard
          label={t('communication.integrations.statusCounts.active')}
          eyebrow={t('communication.integrations.statusCounts.activeEyebrow')}
          value={active}
          tone="active"
          emphasis
        />
      )}
      {showConfigured && (
        <MetricCard
          label={t('communication.integrations.statusCounts.configured')}
          eyebrow={t('communication.integrations.statusCounts.configuredEyebrow')}
          value={configured}
        />
      )}
      {showErrored && (
        <MetricCard
          label={t('communication.integrations.statusCounts.errored')}
          eyebrow={t('communication.integrations.statusCounts.erroredEyebrow')}
          value={errored}
          tone="destructive"
          emphasis
        />
      )}
    </div>
  );
}
