import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import { useInstanceStatusCounts } from '../api/workflow.queries';

interface InstancesStatusHeroProps {
  startedByUserId?: string;
  entityType?: string;
  state?: string;
}

export function InstancesStatusHero({
  startedByUserId,
  entityType,
  state,
}: InstancesStatusHeroProps) {
  const { t } = useTranslation();
  const { data, isLoading } = useInstanceStatusCounts({
    startedByUserId,
    entityType,
    state,
  });

  if (isLoading || !data) return null;

  const cards = [
    {
      key: 'active',
      count: data.active,
      label: t('workflow.instances.statusCounts.active'),
      eyebrow: t('workflow.instances.statusCounts.activeEyebrow'),
      tone: 'active' as const,
      emphasis: data.active > 0,
    },
    {
      key: 'awaiting',
      count: data.awaiting,
      label: t('workflow.instances.statusCounts.awaiting'),
      eyebrow: t('workflow.instances.statusCounts.awaitingEyebrow'),
      tone: 'default' as const,
      emphasis: data.awaiting > 0,
    },
    {
      key: 'completed',
      count: data.completed,
      label: t('workflow.instances.statusCounts.completed'),
      eyebrow: t('workflow.instances.statusCounts.completedEyebrow'),
      tone: 'default' as const,
      emphasis: false,
    },
    {
      key: 'cancelled',
      count: data.cancelled,
      label: t('workflow.instances.statusCounts.cancelled'),
      eyebrow: t('workflow.instances.statusCounts.cancelledEyebrow'),
      tone: 'destructive' as const,
      emphasis: data.cancelled > 0,
    },
  ].filter((card) => card.count > 0);

  if (cards.length === 0) return null;

  return (
    <div
      className={cn(
        'mb-6 grid gap-4',
        cards.length === 1 && 'sm:grid-cols-1',
        cards.length === 2 && 'sm:grid-cols-2',
        cards.length === 3 && 'sm:grid-cols-3',
        cards.length === 4 && 'sm:grid-cols-2 lg:grid-cols-4',
      )}
    >
      {cards.map((card) => (
        <MetricCard
          key={card.key}
          label={card.label}
          eyebrow={card.eyebrow}
          value={card.count}
          tone={card.tone}
          emphasis={card.emphasis}
        />
      ))}
    </div>
  );
}
