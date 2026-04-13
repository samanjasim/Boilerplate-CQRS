import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MessageSquare, Activity, Clock } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/common/EmptyState';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { useTimeline } from '../api';
import type { TimelineItem } from '@/types/comments-activity.types';
import { useEntityChannel } from '../hooks/useEntityChannel';
import { CommentThread } from './CommentThread';
import { ActivityItem } from './ActivityItem';
import { CommentComposer } from './CommentComposer';
import { WatchButton } from './WatchButton';
import { cn } from '@/lib/utils';

type FilterType = 'all' | 'comments' | 'activity';

interface EntityTimelineProps {
  entityType: string;
  entityId: string;
}

export function EntityTimeline({ entityType, entityId }: EntityTimelineProps) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [filter, setFilter] = useState<FilterType>('all');
  const [pageNumber, setPageNumber] = useState(1);
  const pageSize = 20;

  // Real-time updates
  useEntityChannel(entityType, entityId);

  const { data, isLoading } = useTimeline(entityType, entityId, {
    filter: filter === 'all' ? undefined : filter,
    pageNumber,
    pageSize,
  });

  const items: TimelineItem[] = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;
  const canLoadMore = pageNumber < totalPages;

  const canCreate = hasPermission(PERMISSIONS.Comments.Create);

  const filters: { key: FilterType; label: string }[] = [
    { key: 'all', label: t('commentsActivity.filterAll', 'All') },
    { key: 'comments', label: t('commentsActivity.filterComments', 'Comments') },
    { key: 'activity', label: t('commentsActivity.filterActivity', 'Activity') },
  ];

  const handleFilterChange = (f: FilterType) => {
    setFilter(f);
    setPageNumber(1);
  };

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">
          {t('commentsActivity.title', 'Comments & Activity')}
        </CardTitle>
        <WatchButton entityType={entityType} entityId={entityId} />
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Filter toggle */}
        <div className="flex items-center gap-1 rounded-lg bg-secondary p-1">
          {filters.map((f) => (
            <Button
              key={f.key}
              variant={filter === f.key ? 'default' : 'ghost'}
              size="sm"
              onClick={() => handleFilterChange(f.key)}
              className={cn(
                'flex-1',
                filter !== f.key && 'text-muted-foreground',
              )}
            >
              {f.key === 'comments' && <MessageSquare className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />}
              {f.key === 'activity' && <Activity className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />}
              {f.key === 'all' && <Clock className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />}
              {f.label}
            </Button>
          ))}
        </div>

        {/* Loading state */}
        {isLoading && (
          <div className="flex items-center justify-center py-10">
            <Spinner size="md" />
          </div>
        )}

        {/* Empty state */}
        {!isLoading && items.length === 0 && (
          <EmptyState
            icon={MessageSquare}
            title={t('commentsActivity.emptyTitle', 'No comments or activity yet')}
            description={t(
              'commentsActivity.emptyDescription',
              'Be the first to leave a comment on this item.',
            )}
            className="py-12"
          />
        )}

        {/* Timeline items */}
        {!isLoading && items.length > 0 && (
          <div className="divide-y divide-border/30">
            {items.map((item) => {
              if (item.type === 'comment' && item.comment) {
                return (
                  <div key={`comment-${item.comment.id}`}>
                    <CommentThread comment={item.comment} />
                  </div>
                );
              }
              if (item.type === 'activity' && item.activity) {
                return (
                  <div key={`activity-${item.activity.id}`}>
                    <ActivityItem activity={item.activity} />
                  </div>
                );
              }
              return null;
            })}
          </div>
        )}

        {/* Load more */}
        {canLoadMore && !isLoading && (
          <div className="flex justify-center pt-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPageNumber((p) => p + 1)}
            >
              {t('commentsActivity.loadMore', 'Load more')}
            </Button>
          </div>
        )}

        {/* New comment composer */}
        {canCreate && (
          <div className="border-t border-border/30 pt-4">
            <CommentComposer entityType={entityType} entityId={entityId} />
          </div>
        )}
      </CardContent>
    </Card>
  );
}
