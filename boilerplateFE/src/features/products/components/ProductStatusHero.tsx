import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { useProductStatusCounts } from '../api';

interface ProductStatusHeroProps {
  tenantId?: string;
}

export function ProductStatusHero({ tenantId }: ProductStatusHeroProps) {
  const { t } = useTranslation();
  const { data, isLoading } = useProductStatusCounts(tenantId ? { tenantId } : undefined);

  if (isLoading) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <MetricCard label={t('products.hero.drafts')} eyebrow={t('products.hero.draftsEyebrow')} value="-" />
        <MetricCard
          label={t('products.hero.active')}
          eyebrow={t('products.hero.activeEyebrow')}
          value="-"
          tone="active"
        />
        <MetricCard label={t('products.hero.archived')} eyebrow={t('products.hero.archivedEyebrow')} value="-" />
      </div>
    );
  }

  if (!data) return null;

  const cards = [
    {
      key: 'draft',
      count: data.draft,
      label: t('products.hero.drafts'),
      eyebrow: t('products.hero.draftsEyebrow'),
      tone: 'default' as const,
      emphasis: false,
    },
    {
      key: 'active',
      count: data.active,
      label: t('products.hero.active'),
      eyebrow: t('products.hero.activeEyebrow'),
      tone: 'active' as const,
      emphasis: data.active > 0,
    },
    {
      key: 'archived',
      count: data.archived,
      label: t('products.hero.archived'),
      eyebrow: t('products.hero.archivedEyebrow'),
      tone: 'default' as const,
      emphasis: false,
    },
  ].filter((card) => card.count > 0);

  if (cards.length === 0) return null;

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
