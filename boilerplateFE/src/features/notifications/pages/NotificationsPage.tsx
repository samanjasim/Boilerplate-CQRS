import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useTimeAgoFormatter } from '@/hooks';
import { ArrowRight, Bell, CheckCheck } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { PageHeader, Pagination, getPersistedPageSize } from '@/components/common';
import { useNotifications, useUnreadCount, useMarkRead, useMarkAllRead } from '@/features/notifications/api';
import { NOTIFICATION_ICONS } from '@/constants';
import { ROUTES } from '@/config';
import { cn } from '@/lib/utils';
import { groupNotificationsByDate } from '../utils/groupByDate';
import type { Notification } from '@/types';

type FilterType = 'all' | 'unread';

const EMPTY_NOTIFICATIONS: Notification[] = [];

function SegmentButton({
  active,
  label,
  onClick,
}: {
  active: boolean;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'h-8 px-3 rounded-[10px] text-sm motion-safe:transition-colors motion-safe:duration-150',
        active ? 'pill-active' : 'state-hover'
      )}
      aria-pressed={active}
    >
      {label}
    </button>
  );
}

export default function NotificationsPage() {
  const { t } = useTranslation();
  const [filter, setFilter] = useState<FilterType>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const formatTimeAgo = useTimeAgoFormatter();

  const isReadParam = filter === 'unread' ? false : undefined;

  const { data, isLoading, isFetching } = useNotifications({
    pageNumber: page,
    pageSize,
    isRead: isReadParam,
  });
  const { data: allNotificationsMeta } = useNotifications(
    { pageNumber: 1, pageSize: 1 },
    { refetchInterval: false }
  );
  const { data: totalUnread = 0 } = useUnreadCount();
  const { mutate: markRead } = useMarkRead();
  const { mutate: markAllRead } = useMarkAllRead();

  const notifications = data?.data ?? EMPTY_NOTIFICATIONS;
  const pagination = data?.pagination;
  const totalAll =
    filter === 'all'
      ? pagination?.totalCount ?? 0
      : allNotificationsMeta?.pagination?.totalCount ?? 0;
  const groups = useMemo(() => groupNotificationsByDate(notifications), [notifications]);

  const handleNotificationClick = (notification: Notification) => {
    if (!notification.isRead) {
      markRead(notification.id);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('notifications.title')}
        actions={
          <Button asChild variant="ghost" size="sm">
            <Link to={ROUTES.PROFILE} className="gap-1">
              {t('notifications.preferencesLink')}
              <ArrowRight className="h-3.5 w-3.5 ltr:ml-1 rtl:mr-1 rtl:rotate-180" />
            </Link>
          </Button>
        }
      />

      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="inline-flex items-center gap-1 rounded-[12px] border border-border/40 bg-foreground/5 p-1">
          <SegmentButton
            active={filter === 'all'}
            label={t('notifications.filter.all', { count: totalAll })}
            onClick={() => { setFilter('all'); setPage(1); }}
          />
          <SegmentButton
            active={filter === 'unread'}
            label={t('notifications.filter.unread', { count: totalUnread })}
            onClick={() => { setFilter('unread'); setPage(1); }}
          />
        </div>
        <Button variant="outline" size="sm" onClick={() => markAllRead()} disabled={totalUnread === 0}>
          <CheckCheck className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('notifications.markAllRead')}
        </Button>
      </div>

      <Card>
        <CardContent className="py-4">
          {isLoading && !data ? (
            <div className="flex items-center justify-center py-8 text-muted-foreground">
              {t('common.loading')}
            </div>
          ) : notifications.length === 0 && !isFetching ? (
            <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
              <Bell className="h-10 w-10 mb-3 opacity-40" />
              <p>{t('notifications.noNotifications')}</p>
            </div>
          ) : (
            <div className="divide-y">
              {groups.map((group) => (
                <section key={group.key} className="pt-3 first:pt-0">
                  <div className="px-2 pb-1 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">
                    <span className="inline-block w-1 h-1 rounded-full bg-primary/70 me-1.5 align-middle -translate-y-px" />
                    {t(`notifications.groups.${group.key}`)}
                  </div>
                  <div className="divide-y">
                    {group.items.map((notification) => {
                      const Icon = NOTIFICATION_ICONS[notification.type] ?? Bell;
                      const timeAgo = formatTimeAgo(notification.createdAt);

                      return (
                        <div
                          key={notification.id}
                          className={cn(
                            'flex items-start gap-4 py-4 px-2 cursor-pointer rounded-lg transition-colors hover:bg-muted/50',
                            !notification.isRead && 'bg-primary/5'
                          )}
                          onClick={() => handleNotificationClick(notification)}
                        >
                          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-muted">
                            <Icon className="h-5 w-5 text-muted-foreground" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <p
                                className={cn(
                                  'text-sm',
                                  !notification.isRead && 'font-semibold'
                                )}
                              >
                                {notification.title}
                              </p>
                              {!notification.isRead && (
                                <Badge variant="default" className="text-[10px] px-1.5 py-0">
                                  {t('notifications.new')}
                                </Badge>
                              )}
                            </div>
                            <p className="text-sm text-muted-foreground mt-0.5">
                              {notification.message}
                            </p>
                            <p className="text-xs text-muted-foreground mt-1">{timeAgo}</p>
                          </div>
                          {!notification.isRead && (
                            <span className="mt-2 h-2.5 w-2.5 shrink-0 rounded-full bg-primary" />
                          )}
                        </div>
                      );
                    })}
                  </div>
                </section>
              ))}
            </div>
          )}

          {/* Pagination */}
          {pagination && (
            <Pagination
              pagination={pagination}
              onPageChange={setPage}
              onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
