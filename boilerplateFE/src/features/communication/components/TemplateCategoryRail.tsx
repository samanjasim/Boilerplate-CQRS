import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';

export interface TemplateCategoryRailProps {
  categories: Array<{ name: string; count: number }>;
  /** undefined = "All categories" pseudo-row active */
  selectedCategory: string | undefined;
  onSelect: (category: string | undefined) => void;
  totalCount: number;
  variant?: 'rail' | 'chips';
  className?: string;
}

export function TemplateCategoryRail({
  categories,
  selectedCategory,
  onSelect,
  totalCount,
  variant = 'rail',
  className,
}: TemplateCategoryRailProps) {
  const { t } = useTranslation();

  if (variant === 'chips') {
    return (
      <div className={cn('flex flex-wrap gap-2', className)}>
        <Button
          variant={selectedCategory === undefined ? 'default' : 'outline'}
          size="sm"
          onClick={() => onSelect(undefined)}
        >
          {t('communication.templates.allCategories')}
          <span className="ms-1.5 text-xs opacity-70">({totalCount})</span>
        </Button>
        {categories.map((cat) => (
          <Button
            key={cat.name}
            variant={selectedCategory === cat.name ? 'default' : 'outline'}
            size="sm"
            onClick={() => onSelect(cat.name)}
          >
            {cat.name}
            <span className="ms-1.5 text-xs opacity-70">({cat.count})</span>
          </Button>
        ))}
      </div>
    );
  }

  return (
    <nav
      aria-label="Categories"
      className={cn(
        'surface-glass rounded-2xl p-2 sticky',
        'top-[var(--shell-header-h,4rem)]',
        className,
      )}
    >
      <ul className="space-y-0.5">
        {categories.map((cat) => {
          const isActive = selectedCategory === cat.name;
          return (
            <li key={cat.name}>
              <button
                type="button"
                onClick={() => onSelect(cat.name)}
                className={cn(
                  'w-full flex items-center justify-between rounded-lg px-3 py-2 text-sm text-start',
                  'transition-colors',
                  isActive
                    ? 'bg-[var(--active-bg)] text-[var(--active-text)] font-medium'
                    : 'text-foreground hover:bg-[var(--hover-bg)]',
                )}
                aria-current={isActive ? 'page' : undefined}
              >
                <span className="truncate">{cat.name}</span>
                <span
                  className={cn(
                    'rounded-full px-2 py-0.5 text-xs tabular-nums',
                    isActive
                      ? 'bg-[var(--active-text)]/15 text-[var(--active-text)]'
                      : 'bg-muted text-muted-foreground',
                  )}
                  aria-label={t('communication.templates.categoryCount', { count: cat.count })}
                >
                  {cat.count}
                </span>
              </button>
            </li>
          );
        })}
      </ul>

      <Separator className="my-2" />

      <button
        type="button"
        onClick={() => onSelect(undefined)}
        className={cn(
          'w-full flex items-center justify-between rounded-lg px-3 py-2 text-sm text-start',
          'transition-colors',
          selectedCategory === undefined
            ? 'bg-[var(--active-bg)] text-[var(--active-text)] font-medium'
            : 'text-muted-foreground hover:bg-[var(--hover-bg)]',
        )}
        aria-current={selectedCategory === undefined ? 'page' : undefined}
      >
        <span>{t('communication.templates.allCategories')}</span>
        <span
          className={cn(
            'rounded-full px-2 py-0.5 text-xs tabular-nums',
            selectedCategory === undefined
              ? 'bg-[var(--active-text)]/15 text-[var(--active-text)]'
              : 'bg-muted text-muted-foreground',
          )}
          aria-label={t('communication.templates.categoryCount', { count: totalCount })}
        >
          {totalCount}
        </span>
      </button>
    </nav>
  );
}
