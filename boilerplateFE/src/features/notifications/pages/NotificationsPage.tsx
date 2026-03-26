import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDistanceToNow } from 'date-fns';
import { Bell, CheckCheck } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { PageHeader, Pagination, getPersistedPageSize } from '@/components/common';
import { useNotifications, useMarkRead, useMarkAllRead } from '@/features/notifications/api';
import { NOTIFICATION_ICONS } from '@/constants';
import { cn } from '@/lib/utils';
import type { Notification } from '@/types';

type FilterType = 'all' | 'unread';

export default function NotificationsPage() {
  const { t } = useTranslation();
  const [filter, setFilter] = useState<FilterType>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);

  const isReadParam = filter === 'unread' ? false : undefined;

  const { data, isLoading, isFetching } = useNotifications({
    pageNumber: page,
    pageSize,
    isRead: isReadParam,
  });
  const { mutate: markRead } = useMarkRead();
  const { mutate: markAllRead } = useMarkAllRead();

  const notifications = data?.data ?? [];
  const pagination = data?.pagination;

  const handleNotificationClick = (notification: Notification) => {
    if (!notification.isRead) {
      markRead(notification.id);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader title={t('notifications.title')} />

      <div className="flex items-center justify-between">
        <div className="flex gap-2">
          <Button
            variant={filter === 'all' ? 'default' : 'outline'}
            size="sm"
            onClick={() => { setFilter('all'); setPage(1); }}
          >
            {t('notifications.all')}
          </Button>
          <Button
            variant={filter === 'unread' ? 'default' : 'outline'}
            size="sm"
            onClick={() => { setFilter('unread'); setPage(1); }}
          >
            {t('notifications.unread')}
          </Button>
        </div>
        <Button variant="outline" size="sm" onClick={() => markAllRead()}>
          <CheckCheck className="h-4 w-4 mr-1" />
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
              {notifications.map((notification) => {
                const Icon = NOTIFICATION_ICONS[notification.type] || NOTIFICATION_ICONS.default;
                const timeAgo = formatDistanceToNow(new Date(notification.createdAt), {
                  addSuffix: true,
                });

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
