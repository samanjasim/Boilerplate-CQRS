import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import { useInboxStatusCounts } from '../api/workflow.queries';

function HeroSkeleton() {
  return (
    <div className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          aria-hidden
          className="h-[108px] w-full animate-pulse rounded-lg border border-border/50 bg-muted/40"
        />
      ))}
    </div>
  );
}

export function InboxStatusHero() {
  const { t } = useTranslation();
  const { data, isLoading } = useInboxStatusCounts();

  if (isLoading && !data) return <HeroSkeleton />;
  if (!data) return null;

  const cards = [
    {
      key: 'overdue',
      count: data.overdue,
      label: t('workflow.inbox.statusCounts.overdue'),
      eyebrow: t('workflow.inbox.statusCounts.overdueEyebrow'),
      tone: 'destructive' as const,
      emphasis: data.overdue > 0,
    },
    {
      key: 'dueToday',
      count: data.dueToday,
      label: t('workflow.inbox.statusCounts.dueToday'),
      eyebrow: t('workflow.inbox.statusCounts.dueTodayEyebrow'),
      tone: 'active' as const,
      emphasis: data.dueToday > 0,
    },
    {
      key: 'upcoming',
      count: data.upcoming,
      label: t('workflow.inbox.statusCounts.upcoming'),
      eyebrow: t('workflow.inbox.statusCounts.upcomingEyebrow'),
      tone: 'default' as const,
      emphasis: false,
    },
  ].filter((card) => card.count > 0);

  if (cards.length === 0) return null;

  return (
    <div
      className={cn(
        'mb-6 grid gap-4',
        cards.length === 1 && 'sm:grid-cols-1',
        cards.length === 2 && 'sm:grid-cols-2',
        cards.length === 3 && 'sm:grid-cols-2 lg:grid-cols-3',
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
