import { useTranslation } from 'react-i18next';
import { UserAvatar } from './UserAvatar';
import { Badge } from '@/components/ui/badge';

export type SubjectItem = { type: 'User' | 'Role'; id: string; name: string };

type Props = { subjects: SubjectItem[]; max?: number };

export function SubjectStack({ subjects, max = 3 }: Props) {
  const { t } = useTranslation();
  const visible = subjects.slice(0, max);
  const overflow = subjects.length - max;

  return (
    <div className="flex items-center gap-1 flex-wrap">
      {visible.map(s =>
        s.type === 'User' ? (
          <span key={s.id} className="flex items-center gap-1 text-sm">
            <UserAvatar size="xs" firstName={s.name.split(' ')[0]} lastName={s.name.split(' ')[1]} />
            <span className="max-w-[120px] truncate">{s.name}</span>
          </span>
        ) : (
          <Badge key={s.id} variant="secondary" className="text-xs">
            {s.name}
          </Badge>
        ),
      )}
      {overflow > 0 && (
        <span className="text-xs text-muted-foreground">
          {t('access.subjectStack.overflow', { count: overflow })}
        </span>
      )}
    </div>
  );
}
