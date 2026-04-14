import { forwardRef, useEffect, useImperativeHandle, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AtSign, Loader2 } from 'lucide-react';
import { UserAvatar } from '@/components/common';
import { useDebounce } from '@/hooks';
import { cn } from '@/lib/utils';
import { useMentionableUsers } from '../api';
import type { MentionableUser } from '@/types/comments-activity.types';

interface MentionAutocompleteProps {
  search: string;
  onSelect: (user: MentionableUser) => void;
  onClose: () => void;
  visible: boolean;
  entityType?: string;
  entityId?: string;
  position?: { top: number; left: number; placement?: 'top' | 'bottom' };
}

export interface MentionAutocompleteHandle {
  moveUp: () => void;
  moveDown: () => void;
  selectActive: () => boolean;
}

function highlightMatch(text: string, query: string) {
  if (!query) return text;
  const lower = text.toLowerCase();
  const index = lower.indexOf(query.toLowerCase());
  if (index < 0) return text;
  return (
    <>
      {text.slice(0, index)}
      <mark className="bg-transparent font-semibold text-primary">
        {text.slice(index, index + query.length)}
      </mark>
      {text.slice(index + query.length)}
    </>
  );
}

export const MentionAutocomplete = forwardRef<MentionAutocompleteHandle, MentionAutocompleteProps>(
  function MentionAutocomplete(
    { search, onSelect, onClose, visible, entityType, entityId, position },
    ref,
  ) {
    const { t } = useTranslation();
    const [activeIndex, setActiveIndex] = useState(0);
    const debouncedSearch = useDebounce(search, 150);

    const { data, isLoading, isFetching } = useMentionableUsers(
      debouncedSearch,
      visible,
      entityType,
      entityId,
    );

    const users: MentionableUser[] = useMemo(() => {
      const raw = Array.isArray(data) ? data : (data?.data ?? []);
      return raw as MentionableUser[];
    }, [data]);

    useEffect(() => {
      setActiveIndex(0);
    }, [debouncedSearch, users.length]);

    useImperativeHandle(
      ref,
      () => ({
        moveUp: () =>
          setActiveIndex((i) => (users.length === 0 ? 0 : (i - 1 + users.length) % users.length)),
        moveDown: () =>
          setActiveIndex((i) => (users.length === 0 ? 0 : (i + 1) % users.length)),
        selectActive: () => {
          const picked = users[activeIndex];
          if (!picked) return false;
          onSelect(picked);
          return true;
        },
      }),
      [users, activeIndex, onSelect],
    );

    if (!visible) return null;

    const showSpinner = isLoading || (isFetching && users.length === 0);

    return (
      <div
        role="listbox"
        aria-label={t('commentsActivity.mentionSomeone', 'Mention someone')}
        className={cn(
          'absolute z-50 w-72 max-w-[calc(100vw-2rem)] overflow-hidden rounded-xl border border-border/40 bg-popover shadow-float',
          position?.placement === 'top' && '-translate-y-full',
        )}
        style={position ? { top: position.top, left: position.left } : undefined}
        onMouseDown={(e) => e.preventDefault()}
      >
        {/* Header */}
        <div className="flex items-center gap-2 border-b border-border/30 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          <AtSign className="h-3 w-3" />
          <span className="flex-1">{t('commentsActivity.mentionSomeone', 'Mention someone')}</span>
          {isFetching && users.length > 0 && (
            <Loader2 className="h-3 w-3 animate-spin text-muted-foreground/60" />
          )}
        </div>

        {/* Body */}
        <div className="max-h-64 overflow-y-auto py-1">
          {showSpinner && (
            <div className="flex items-center justify-center gap-2 px-3 py-6 text-xs text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t('common.loading', 'Loading...')}
            </div>
          )}

          {!showSpinner && users.length === 0 && (
            <div className="flex flex-col items-center gap-1 px-3 py-6 text-center">
              <AtSign className="h-5 w-5 text-muted-foreground/40" />
              <p className="text-sm font-medium text-foreground">
                {t('commentsActivity.noUsersFound', 'No users found')}
              </p>
              <p className="text-xs text-muted-foreground">
                {search
                  ? t('commentsActivity.tryDifferentSearch', 'Try a different name or email')
                  : t('commentsActivity.startTypingToSearch', 'Start typing to search users')}
              </p>
            </div>
          )}

          {!showSpinner &&
            users.map((user, i) => {
              const nameParts = user.displayName.trim().split(/\s+/);
              const first = nameParts[0];
              const last = nameParts.length > 1 ? nameParts[nameParts.length - 1] : undefined;
              const isActive = i === activeIndex;

              return (
                <button
                  key={user.id}
                  type="button"
                  role="option"
                  aria-selected={isActive}
                  onMouseEnter={() => setActiveIndex(i)}
                  onClick={() => {
                    onSelect(user);
                    onClose();
                  }}
                  className={cn(
                    'flex w-full items-center gap-2.5 px-3 py-2 text-start transition-colors',
                    isActive
                      ? '[background:var(--active-bg)] [color:var(--active-text)]'
                      : 'hover:bg-secondary',
                  )}
                >
                  <UserAvatar firstName={first} lastName={last} size="sm" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-foreground">
                      {highlightMatch(user.displayName, debouncedSearch)}
                    </p>
                    <p className="truncate text-xs text-muted-foreground">
                      @{user.username}
                      <span className="mx-1 text-muted-foreground/50">·</span>
                      {user.email}
                    </p>
                  </div>
                </button>
              );
            })}
        </div>

        {/* Footer — keyboard hints */}
        {!showSpinner && users.length > 0 && (
          <div className="flex items-center gap-3 border-t border-border/30 bg-muted/30 px-3 py-1.5 text-[10px] text-muted-foreground">
            <span className="flex items-center gap-1">
              <kbd className="rounded border border-border/50 bg-background px-1 font-mono text-[9px]">
                ↑
              </kbd>
              <kbd className="rounded border border-border/50 bg-background px-1 font-mono text-[9px]">
                ↓
              </kbd>
              {t('commentsActivity.navigate', 'navigate')}
            </span>
            <span className="flex items-center gap-1">
              <kbd className="rounded border border-border/50 bg-background px-1 font-mono text-[9px]">
                ↵
              </kbd>
              {t('commentsActivity.select', 'select')}
            </span>
            <span className="flex items-center gap-1">
              <kbd className="rounded border border-border/50 bg-background px-1 font-mono text-[9px]">
                esc
              </kbd>
              {t('commentsActivity.dismiss', 'dismiss')}
            </span>
          </div>
        )}
      </div>
    );
  },
);
