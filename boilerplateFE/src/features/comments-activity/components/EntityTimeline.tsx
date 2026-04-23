import { useState, useCallback, useMemo } from 'react';
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
  tenantId?: string;
}

export function EntityTimeline({ entityType, entityId, tenantId }: EntityTimelineProps) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [filter, setFilter] = useState<FilterType>('all');
  const [pageNumber, setPageNumber] = useState(1);
  const [accumulatedItems, setAccumulatedItems] = useState<TimelineItem[]>([]);
  const pageSize = 20;

  // Real-time updates
  useEntityChannel(entityType, entityId, tenantId);

  const { data, isLoading, isFetching } = useTimeline(entityType, entityId, {
    filter: filter === 'all' ? undefined : filter,
    pageNumber,
    pageSize,
  });

  // Accumulate items across pages; reset when filter changes or page is 1.
  // Adjust-state-in-render pattern — we sync with the fetched page as soon
  // as React renders with a new `data` reference, without the extra pass of
  // an effect.
  const [lastSync, setLastSync] = useState<{
    data: TimelineItem[] | undefined;
    filter: FilterType;
    page: number;
  }>({ data: undefined, filter, page: pageNumber });

  if (
    data?.data &&
    (lastSync.data !== data.data || lastSync.filter !== filter || lastSync.page !== pageNumber)
  ) {
    const filterChanged = lastSync.filter !== filter;
    const incoming = data.data;
    setLastSync({ data: incoming, filter, page: pageNumber });
    if (pageNumber === 1 || filterChanged) {
      setAccumulatedItems(incoming);
    } else {
      setAccumulatedItems((prev) => {
        const existingIds = new Set(
          prev.map((item) =>
            item.type === 'comment' ? `c-${item.comment?.id}` : `a-${item.activity?.id}`,
          ),
        );
        const newItems = incoming.filter((item: TimelineItem) => {
          const key =
            item.type === 'comment' ? `c-${item.comment?.id}` : `a-${item.activity?.id}`;
          return !existingIds.has(key);
        });
        return [...prev, ...newItems];
      });
    }
  }

  const totalPages = data?.pagination?.totalPages ?? 1;
  const canLoadMore = pageNumber < totalPages;
  const canCreate = hasPermission(PERMISSIONS.Comments.Create);

  const filters = useMemo<{ key: FilterType; label: string }[]>(
    () => [
      { key: 'all', label: t('commentsActivity.filterAll', 'All') },
      { key: 'comments', label: t('commentsActivity.filterComments', 'Comments') },
      { key: 'activity', label: t('commentsActivity.filterActivity', 'Activity') },
    ],
    [t],
  );

  const handleFilterChange = useCallback((f: FilterType) => {
    setFilter(f);
    setPageNumber(1);
    setAccumulatedItems([]);
  }, []);

  // ArrowLeft / ArrowRight on the tablist moves focus + selection to the
  // neighbouring tab, Home/End jump to the ends. Matches WAI-ARIA tab pattern.
  const handleTablistKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLDivElement>) => {
      const keys = ['ArrowLeft', 'ArrowRight', 'Home', 'End'];
      if (!keys.includes(event.key)) return;
      event.preventDefault();
      const currentIndex = filters.findIndex((f) => f.key === filter);
      let nextIndex = currentIndex;
      if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % filters.length;
      if (event.key === 'ArrowLeft') nextIndex = (currentIndex - 1 + filters.length) % filters.length;
      if (event.key === 'Home') nextIndex = 0;
      if (event.key === 'End') nextIndex = filters.length - 1;
      const next = filters[nextIndex];
      handleFilterChange(next.key);
      const tab = event.currentTarget.querySelector<HTMLButtonElement>(
        `[data-filter-key="${next.key}"]`,
      );
      tab?.focus();
    },
    [filter, filters, handleFilterChange],
  );

  const handleLoadMore = useCallback(() => {
    setPageNumber((p) => p + 1);
  }, []);

  const showEmpty = !isLoading && accumulatedItems.length === 0;
  const showItems = accumulatedItems.length > 0;

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
        <div
          role="tablist"
          aria-label={t('commentsActivity.filterLabel', 'Timeline filter')}
          onKeyDown={handleTablistKeyDown}
          className="flex items-center gap-1 rounded-lg bg-secondary p-1"
        >
          {filters.map((f) => {
            const selected = filter === f.key;
            return (
              <Button
                key={f.key}
                role="tab"
                aria-selected={selected}
                tabIndex={selected ? 0 : -1}
                data-filter-key={f.key}
                variant={selected ? 'default' : 'ghost'}
                size="sm"
                onClick={() => handleFilterChange(f.key)}
                className={cn('flex-1', !selected && 'text-muted-foreground')}
              >
                {f.key === 'comments' && <MessageSquare className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />}
                {f.key === 'activity' && <Activity className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />}
                {f.key === 'all' && <Clock className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />}
                {f.label}
              </Button>
            );
          })}
        </div>

        {/* Loading state — only for initial load */}
        {isLoading && accumulatedItems.length === 0 && (
          <div className="flex items-center justify-center py-10">
            <Spinner size="md" />
          </div>
        )}

        {/* Empty state */}
        {showEmpty && (
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
        {showItems && (
          <div className="divide-y divide-border/30">
            {accumulatedItems.map((item) => {
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
              onClick={handleLoadMore}
              disabled={isFetching}
            >
              {isFetching
                ? t('common.loading', 'Loading...')
                : t('commentsActivity.loadMore', 'Load more')}
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
