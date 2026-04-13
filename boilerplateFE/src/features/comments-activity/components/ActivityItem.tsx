import { formatDistanceToNow } from 'date-fns';
import { useTranslation } from 'react-i18next';
import type { ActivityEntry } from '@/types/comments-activity.types';

const DOT_COLORS: Record<string, string> = {
  created: 'bg-green-500',
  updated: 'bg-blue-500',
  deleted: 'bg-red-500',
  comment_added: 'bg-muted-foreground/50',
};

function getDotColor(action: string): string {
  return DOT_COLORS[action] ?? 'bg-muted-foreground/50';
}

interface ActivityItemProps {
  activity: ActivityEntry;
}

export function ActivityItem({ activity }: ActivityItemProps) {
  const { t } = useTranslation();
  const timeAgo = formatDistanceToNow(new Date(activity.createdAt), { addSuffix: true });

  return (
    <div className="flex items-start gap-3 py-2">
      <div className="mt-1.5 flex shrink-0 items-center justify-center">
        <span className={`block h-2.5 w-2.5 rounded-full ${getDotColor(activity.action)}`} />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm text-foreground">
          {activity.description ?? t('commentsActivity.activityAction', { action: activity.action })}
        </p>
        <div className="mt-0.5 flex items-center gap-2 text-xs text-muted-foreground">
          {activity.actorName && <span className="font-medium">{activity.actorName}</span>}
          <span>{timeAgo}</span>
        </div>
      </div>
    </div>
  );
}
