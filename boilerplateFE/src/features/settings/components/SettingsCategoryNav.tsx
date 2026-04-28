import type { KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

export interface CategoryNavItem {
  category: string;
  label: string;
  count?: number;
}

interface SettingsCategoryNavProps {
  items: CategoryNavItem[];
  activeCategory: string;
  onSelect: (category: string) => void;
  className?: string;
}

export function SettingsCategoryNav({
  items,
  activeCategory,
  onSelect,
  className,
}: SettingsCategoryNavProps) {
  const { t } = useTranslation();

  const handleKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
    const idx = items.findIndex((item) => item.category === activeCategory);
    if (idx === -1) return;

    const nextIdx = event.key === 'ArrowDown'
      ? Math.min(items.length - 1, idx + 1)
      : Math.max(0, idx - 1);
    const nextItem = items[nextIdx];
    if (nextItem) onSelect(nextItem.category);
    event.preventDefault();
  };

  return (
    <>
      <nav
        aria-label={t('settings.categoriesNav')}
        className={cn('flex gap-2 overflow-x-auto pb-2 lg:hidden', className)}
      >
        {items.map((item) => (
          <button
            key={item.category}
            type="button"
            aria-current={item.category === activeCategory ? 'true' : undefined}
            onClick={() => onSelect(item.category)}
            className={cn(
              'shrink-0 rounded-lg px-3 py-1.5 text-sm whitespace-nowrap transition-colors',
              item.category === activeCategory
                ? 'bg-[var(--active-bg)] text-[var(--active-text)]'
                : 'text-muted-foreground hover:bg-[var(--hover-bg)]'
            )}
          >
            {item.label}
            {typeof item.count === 'number' && (
              <span className="ms-2 text-xs opacity-70">{item.count}</span>
            )}
          </button>
        ))}
      </nav>

      <nav
        aria-label={t('settings.categoriesNav')}
        className={cn(
          'sticky top-24 hidden self-start bottom-[var(--settings-save-bar-h,0px)] lg:flex lg:flex-col lg:gap-1',
          className
        )}
        onKeyDown={handleKeyDown}
      >
        {items.map((item) => (
          <button
            key={item.category}
            type="button"
            aria-current={item.category === activeCategory ? 'true' : undefined}
            onClick={() => onSelect(item.category)}
            className={cn(
              'rounded-lg px-3 py-2 text-start text-sm transition-colors',
              item.category === activeCategory
                ? 'bg-[var(--active-bg)] font-medium text-[var(--active-text)]'
                : 'text-muted-foreground hover:bg-[var(--hover-bg)]'
            )}
          >
            {item.label}
            {typeof item.count === 'number' && (
              <span className="ms-2 text-xs opacity-70">{item.count}</span>
            )}
          </button>
        ))}
      </nav>
    </>
  );
}
