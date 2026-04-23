import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useSearchUsers } from '@/features/users/api/users.queries';
import { useRoles } from '@/features/roles/api/roles.queries';

export type SubjectSelection = { type: 'User' | 'Role'; id: string; name: string };

type Props = {
  mode?: 'all' | 'user-only';
  excludeIds?: string[];
  onSelect: (s: SubjectSelection) => void;
};

export function SubjectPicker({ mode = 'all', excludeIds = [], onSelect }: Props) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [tab, setTab] = useState<'users' | 'roles'>('users');

  const { data: usersData } = useSearchUsers(
    { searchTerm: search, pageSize: 20, pageNumber: 1 },
    { enabled: true },
  );
  const { data: rolesData } = useRoles({ enabled: mode === 'all' });

  const users = (usersData?.data ?? []).filter((u: { id: string }) => !excludeIds.includes(u.id));
  const roles = (rolesData?.data ?? []).filter((r: { id: string }) => !excludeIds.includes(r.id));

  return (
    <div className="space-y-2">
      <div className="relative">
        <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input
          className="pl-8"
          placeholder={t('access.subjectPicker.search')}
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
      </div>

      {mode !== 'user-only' && (
        <div className="flex gap-1 border-b">
          {(['users', 'roles'] as const).map(t2 => (
            <button
              key={t2}
              type="button"
              className={cn(
                'px-3 py-1.5 text-sm -mb-px border-b-2 transition-colors',
                tab === t2
                  ? 'border-primary text-foreground font-medium'
                  : 'border-transparent text-muted-foreground hover:text-foreground',
              )}
              onClick={() => setTab(t2)}
            >
              {t2 === 'users' ? t('access.subjectPicker.users') : t('access.subjectPicker.roles')}
            </button>
          ))}
        </div>
      )}

      <ul className="max-h-48 overflow-y-auto rounded-md border divide-y text-sm">
        {tab === 'users' || mode === 'user-only' ? (
          <>
            {users.length === 0 && (
              <li className="px-3 py-2 text-muted-foreground">{t('common.noResults')}</li>
            )}
            {users.map((u: { id: string; fullName?: string; email: string }) => (
              <li key={u.id}>
                <Button
                  variant="ghost"
                  className="w-full justify-start rounded-none px-3 py-2 h-auto"
                  onClick={() => onSelect({ type: 'User', id: u.id, name: u.fullName ?? u.email })}
                >
                  {u.fullName ?? u.email}
                </Button>
              </li>
            ))}
          </>
        ) : (
          <>
            {roles.length === 0 && (
              <li className="px-3 py-2 text-muted-foreground">{t('common.noResults')}</li>
            )}
            {roles.map((r: { id: string; name: string }) => (
              <li key={r.id}>
                <Button
                  variant="ghost"
                  className="w-full justify-start rounded-none px-3 py-2 h-auto"
                  onClick={() => onSelect({ type: 'Role', id: r.id, name: r.name })}
                >
                  {r.name}
                </Button>
              </li>
            ))}
          </>
        )}
      </ul>
    </div>
  );
}
