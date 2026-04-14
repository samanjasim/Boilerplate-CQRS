import { useTranslation } from 'react-i18next';
import { Eye, EyeOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useWatchStatus, useWatch, useUnwatch } from '../api';

interface WatchButtonProps {
  entityType: string;
  entityId: string;
}

export function WatchButton({ entityType, entityId }: WatchButtonProps) {
  const { t } = useTranslation();
  const { data: watchStatus } = useWatchStatus(entityType, entityId);
  const { mutate: watch, isPending: isWatching } = useWatch();
  const { mutate: unwatch, isPending: isUnwatching } = useUnwatch();
  const isPending = isWatching || isUnwatching;

  const isCurrentlyWatching = watchStatus?.data?.isWatching ?? false;
  const watcherCount = watchStatus?.data?.watcherCount ?? 0;

  const handleToggle = () => {
    if (isCurrentlyWatching) {
      unwatch({ entityType, entityId });
    } else {
      watch({ entityType, entityId });
    }
  };

  return (
    <div className="flex items-center gap-2">
      <Button
        variant="outline"
        size="sm"
        onClick={handleToggle}
        disabled={isPending}
      >
        {isCurrentlyWatching ? (
          <EyeOff className="ltr:mr-1.5 rtl:ml-1.5 h-4 w-4" />
        ) : (
          <Eye className="ltr:mr-1.5 rtl:ml-1.5 h-4 w-4" />
        )}
        {isCurrentlyWatching
          ? t('commentsActivity.watching', 'Watching')
          : t('commentsActivity.watch', 'Watch')}
      </Button>
      {watcherCount > 0 && (
        <Badge
          variant="secondary"
          aria-label={t('commentsActivity.watchersCount', { count: watcherCount })}
        >
          {watcherCount}
        </Badge>
      )}
    </div>
  );
}
