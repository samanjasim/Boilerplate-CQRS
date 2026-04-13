import { useTranslation } from 'react-i18next';
import { useMentionableUsers } from '../api';
import type { MentionableUser } from '@/types/comments-activity.types';

interface MentionAutocompleteProps {
  search: string;
  onSelect: (user: MentionableUser) => void;
  visible: boolean;
  position?: { top: number; left: number };
}

export function MentionAutocomplete({ search, onSelect, visible, position }: MentionAutocompleteProps) {
  const { t } = useTranslation();
  const { data, isLoading } = useMentionableUsers(search, visible && search.length > 0);

  if (!visible) return null;

  const users: MentionableUser[] = Array.isArray(data) ? data : data?.data ?? [];

  return (
    <div
      className="absolute z-50 max-h-48 w-64 overflow-y-auto rounded-xl border border-border/30 bg-popover p-1 shadow-float"
      style={position ? { top: position.top, left: position.left } : undefined}
    >
      {isLoading && (
        <div className="px-3 py-2 text-sm text-muted-foreground">
          {t('common.loading', 'Loading...')}
        </div>
      )}

      {!isLoading && users.length === 0 && (
        <div className="px-3 py-2 text-sm text-muted-foreground">
          {t('commentsActivity.noUsersFound', 'No users found')}
        </div>
      )}

      {users.map((user) => (
        <button
          key={user.id}
          type="button"
          onClick={() => onSelect(user)}
          className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-start text-sm transition-colors duration-150 hover:bg-secondary"
        >
          <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary text-[10px] font-semibold text-primary-foreground">
            {user.displayName.charAt(0).toUpperCase()}
          </div>
          <div className="min-w-0 flex-1">
            <p className="truncate font-medium text-foreground">{user.displayName}</p>
            <p className="truncate text-xs text-muted-foreground">{user.email}</p>
          </div>
        </button>
      ))}
    </div>
  );
}
