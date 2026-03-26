import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { PaginationMeta } from '@/types';

const STORAGE_KEY = 'app-page-size';
const DEFAULT_PAGE_SIZE = 20;

/** Read persisted page size from localStorage */
export function getPersistedPageSize(): number {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) return Number(stored);
  } catch { /* ignore */ }
  return DEFAULT_PAGE_SIZE;
}

/** Persist page size to localStorage */
function persistPageSize(size: number) {
  try {
    localStorage.setItem(STORAGE_KEY, String(size));
  } catch { /* ignore */ }
}

interface PaginationProps {
  pagination: PaginationMeta;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  pageSizeOptions?: number[];
  className?: string;
}

export function Pagination({
  pagination,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 20, 50],
  className,
}: PaginationProps) {
  const { t } = useTranslation();
  const { pageNumber, pageSize, totalPages, totalCount, hasNextPage, hasPreviousPage } = pagination;

  // Don't render pagination when there's no data
  if (totalCount === 0 || totalPages === 0) return null;

  const start = (pageNumber - 1) * pageSize + 1;
  const end = Math.min(pageNumber * pageSize, totalCount);

  const handlePageSizeChange = (size: number) => {
    persistPageSize(size);
    onPageSizeChange?.(size);
  };

  // Generate visible page numbers (max 5, centered on current)
  const getPageNumbers = (): (number | '...')[] => {
    if (totalPages <= 5) {
      return Array.from({ length: totalPages }, (_, i) => i + 1);
    }
    if (pageNumber <= 3) {
      return [1, 2, 3, 4, '...', totalPages];
    }
    if (pageNumber >= totalPages - 2) {
      return [1, '...', totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
    }
    return [1, '...', pageNumber - 1, pageNumber, pageNumber + 1, '...', totalPages];
  };

  return (
    <div className={cn('flex items-center justify-between gap-4', className)}>
      {/* Left: showing count + page size */}
      <div className="flex items-center gap-3">
        <p className="text-sm text-muted-foreground whitespace-nowrap">
          {t('common.showing', { start, end, total: totalCount })}
        </p>
        {onPageSizeChange && (
          <Select value={String(pageSize)} onValueChange={(v) => handlePageSizeChange(Number(v))}>
            <SelectTrigger className="h-8 w-[70px] text-xs">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {pageSizeOptions.map((size) => (
                <SelectItem key={size} value={String(size)}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </div>

      {/* Right: page numbers — always visible */}
      <div className="flex items-center gap-1.5">
        <button
          onClick={() => onPageChange(pageNumber - 1)}
          disabled={!hasPreviousPage}
          className="flex h-8 w-8 items-center justify-center rounded-lg border border-border text-muted-foreground transition-colors hover:bg-secondary disabled:opacity-30 disabled:pointer-events-none"
        >
          <ChevronLeft className="h-4 w-4 rtl:rotate-180" />
        </button>

        {getPageNumbers().map((page, i) =>
          page === '...' ? (
            <span key={`dots-${i}`} className="px-1 text-sm text-muted-foreground select-none">...</span>
          ) : (
            <button
              key={page}
              onClick={() => onPageChange(page)}
              className={cn(
                'flex h-8 w-8 items-center justify-center rounded-lg text-sm font-medium transition-colors',
                page === pageNumber
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-secondary'
              )}
            >
              {page}
            </button>
          )
        )}

        <button
          onClick={() => onPageChange(pageNumber + 1)}
          disabled={!hasNextPage}
          className="flex h-8 w-8 items-center justify-center rounded-lg border border-border text-muted-foreground transition-colors hover:bg-secondary disabled:opacity-30 disabled:pointer-events-none"
        >
          <ChevronRight className="h-4 w-4 rtl:rotate-180" />
        </button>
      </div>
    </div>
  );
}
