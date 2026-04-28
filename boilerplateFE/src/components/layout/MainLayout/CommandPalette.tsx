import * as DialogPrimitive from '@radix-ui/react-dialog';
import { History, Search } from 'lucide-react';
import { useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import { cn } from '@/lib/utils';
import {
  selectCommandPaletteOpen,
  selectRecentRoutes,
  useUIStore,
} from '@/stores';

import { useNavGroups, type SidebarNavItem } from './useNavGroups';

interface PaletteRow {
  item: SidebarNavItem;
  groupLabel?: string;
}

export function CommandPalette() {
  const { t } = useTranslation();
  const open = useUIStore(selectCommandPaletteOpen);
  const setOpen = useUIStore((s) => s.setCommandPaletteOpen);
  const recentRoutes = useUIStore(selectRecentRoutes);
  const pushRecentRoute = useUIStore((s) => s.pushRecentRoute);
  const navigate = useNavigate();
  const groups = useNavGroups();

  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  const handleOpenChange = (next: boolean) => {
    if (next) {
      setQuery('');
      setActiveIndex(0);
    }
    setOpen(next);
  };

  // Flatten nav into rows tagged with group label.
  const allRows = useMemo<PaletteRow[]>(() => {
    const rows: PaletteRow[] = [];
    for (const g of groups) {
      for (const item of g.items) {
        rows.push({ item, groupLabel: g.label });
      }
    }
    return rows;
  }, [groups]);

  // Filtered rows for the current query (case-insensitive on label + group).
  const filtered = useMemo<PaletteRow[]>(() => {
    const q = query.trim().toLowerCase();
    if (!q) return [];
    return allRows.filter(({ item, groupLabel }) => {
      const hay = `${item.label} ${groupLabel ?? ''}`.toLowerCase();
      return hay.includes(q);
    });
  }, [allRows, query]);

  // Recent rows resolve cached paths back to currently-visible items.
  const recentRows = useMemo<PaletteRow[]>(() => {
    if (query.trim()) return [];
    const byPath = new Map(allRows.map((r) => [r.item.path, r]));
    return recentRoutes
      .map((path) => byPath.get(path))
      .filter((r): r is PaletteRow => Boolean(r));
  }, [allRows, recentRoutes, query]);

  const visibleRows = query.trim() ? filtered : recentRows;
  // Clamp during render so a shrinking list never points past the end.
  const safeActiveIndex =
    visibleRows.length === 0 ? 0 : Math.min(activeIndex, visibleRows.length - 1);

  const commit = (row: PaletteRow) => {
    pushRecentRoute(row.item.path);
    setOpen(false);
    navigate(row.item.path);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActiveIndex(
        visibleRows.length === 0 ? 0 : (safeActiveIndex + 1) % visibleRows.length
      );
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIndex(
        visibleRows.length === 0
          ? 0
          : (safeActiveIndex - 1 + visibleRows.length) % visibleRows.length
      );
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const row = visibleRows[safeActiveIndex];
      if (row) commit(row);
    }
  };

  return (
    <DialogPrimitive.Root open={open} onOpenChange={handleOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay
          className={cn(
            'fixed inset-0 z-50 bg-black/40 backdrop-blur-sm',
            'data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0'
          )}
        />
        <DialogPrimitive.Content
          onOpenAutoFocus={(e) => {
            e.preventDefault();
            inputRef.current?.focus();
          }}
          aria-label={t('command_palette.title')}
          className={cn(
            'fixed left-1/2 top-[18%] z-50 w-[92vw] max-w-xl -translate-x-1/2',
            'surface-glass-strong rounded-2xl shadow-float overflow-hidden',
            'data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95'
          )}
        >
          <DialogPrimitive.Title className="sr-only">
            {t('command_palette.title')}
          </DialogPrimitive.Title>
          <DialogPrimitive.Description className="sr-only">
            {t('command_palette.description')}
          </DialogPrimitive.Description>

          {/* Search input row */}
          <div className="flex items-center gap-2 border-b border-border/40 px-4 py-3">
            <Search className="h-4 w-4 text-muted-foreground" />
            <input
              ref={inputRef}
              type="text"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setActiveIndex(0);
              }}
              onKeyDown={onKeyDown}
              placeholder={t('command_palette.placeholder')}
              className={cn(
                'flex-1 bg-transparent text-sm text-foreground placeholder:text-muted-foreground',
                'outline-none border-0'
              )}
            />
            <span className="hidden sm:inline rounded-md border border-foreground/15 bg-foreground/5 px-1.5 py-0.5 font-mono text-[10px] tracking-[0.05em] text-muted-foreground">
              ESC
            </span>
          </div>

          {/* Results */}
          <div className="max-h-[60vh] overflow-y-auto p-2">
            {!query.trim() && recentRows.length > 0 && (
              <div className="px-2 pt-2 pb-1 flex items-center gap-1.5 text-[10px] font-medium uppercase tracking-[0.08em] text-muted-foreground">
                <History className="h-3 w-3" />
                {t('command_palette.recent')}
              </div>
            )}

            {visibleRows.length === 0 ? (
              <div className="px-3 py-10 text-center text-sm text-muted-foreground">
                {query.trim()
                  ? t('command_palette.noResults', { query: query.trim() })
                  : t('command_palette.empty')}
              </div>
            ) : (
              <ul role="listbox" className="flex flex-col gap-0.5">
                {visibleRows.map((row, idx) => {
                  const Icon = row.item.icon;
                  const isActive = idx === safeActiveIndex;
                  return (
                    <li key={`${row.item.path}-${idx}`} role="option" aria-selected={isActive}>
                      <button
                        type="button"
                        onMouseEnter={() => setActiveIndex(idx)}
                        onClick={() => commit(row)}
                        className={cn(
                          'w-full flex items-center gap-3 rounded-lg px-3 py-2.5 text-start',
                          'motion-safe:transition-colors motion-safe:duration-100',
                          isActive
                            ? 'bg-[var(--active-bg)] text-[var(--active-text)]'
                            : 'text-foreground hover:bg-[var(--hover-bg)]'
                        )}
                      >
                        <Icon
                          className={cn(
                            'h-4 w-4 shrink-0',
                            isActive ? 'text-[var(--active-text)]' : 'text-muted-foreground'
                          )}
                        />
                        <span className="flex-1 truncate text-sm font-medium">
                          {row.item.label}
                        </span>
                        {row.groupLabel && (
                          <span className="shrink-0 text-[10px] font-medium uppercase tracking-[0.08em] text-muted-foreground">
                            {row.groupLabel}
                          </span>
                        )}
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
