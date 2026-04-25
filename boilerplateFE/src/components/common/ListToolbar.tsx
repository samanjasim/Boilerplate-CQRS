import type { ReactNode } from 'react';
import { Search } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

interface ListToolbarProps {
  /** Search input config. Omit to hide the search field. */
  search?: {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
  };
  /** Filter controls (selects, date pickers). Rendered after the search input. */
  filters?: ReactNode;
  /** Right-aligned action buttons. */
  actions?: ReactNode;
  className?: string;
}

/**
 * Standard list-page toolbar: search input + filters + right-aligned actions.
 * Uses logical (`ps-`/`pe-`) padding so the search icon flips correctly under RTL.
 */
export function ListToolbar({ search, filters, actions, className }: ListToolbarProps) {
  const { t } = useTranslation();

  return (
    <div className={cn('flex flex-wrap items-center gap-3', className)}>
      {search && (
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder={search.placeholder ?? t('common.search')}
            value={search.value}
            onChange={(e) => search.onChange(e.target.value)}
            className="ps-9"
          />
        </div>
      )}
      {filters && <div className="flex flex-wrap items-center gap-2">{filters}</div>}
      {actions && <div className="ms-auto flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
