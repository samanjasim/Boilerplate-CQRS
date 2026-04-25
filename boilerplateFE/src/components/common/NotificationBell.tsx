import { Bell, CheckCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { useTimeAgo } from '@/hooks';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useUnreadCount, useNotifications, useMarkRead, useMarkAllRead } from '@/features/notifications/api';
import { useAblyNotifications } from '@/hooks/useAblyNotifications';
import { NOTIFICATION_ICONS } from '@/constants';
import { ROUTES } from '@/config';
import { cn } from '@/lib/utils';
import type { Notification } from '@/types';

function NotificationIcon({ type }: { type: string }) {
  const Icon = NOTIFICATION_ICONS[type] ?? Bell;
  return <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />;
}

function NotificationItem({
  notification,
  onRead,
}: {
  notification: Notification;
  onRead: (n: Notification) => void;
}) {
  const timeAgo = useTimeAgo(notification.createdAt);

  return (
    <DropdownMenuItem
      className="flex items-start gap-3 p-3 cursor-pointer"
      onClick={() => onRead(notification)}
    >
      <NotificationIcon type={notification.type} />
      <div className="flex-1 min-w-0">
        <p
          className={cn(
            'text-sm leading-tight',
            !notification.isRead && 'font-semibold'
          )}
        >
          {notification.title}
        </p>
        <p className="text-xs text-muted-foreground mt-0.5 line-clamp-2">
          {notification.message}
        </p>
        <p className="text-xs text-muted-foreground mt-1">{timeAgo}</p>
      </div>
      {!notification.isRead && (
        <span className="mt-1 h-2 w-2 shrink-0 rounded-full bg-primary" />
      )}
    </DropdownMenuItem>
  );
}

export function NotificationBell() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { connected: ablyConnected } = useAblyNotifications();
  const { data: unreadCount = 0 } = useUnreadCount({
    refetchInterval: ablyConnected ? false : 30000,
  });
  const { data: notificationsData } = useNotifications(
    { pageSize: 5 },
    { refetchInterval: ablyConnected ? false : 30000 }
  );
  const { mutate: markRead } = useMarkRead();
  const { mutate: markAllRead } = useMarkAllRead();

  const notifications = notificationsData?.data ?? [];

  const handleNotificationClick = (notification: Notification) => {
    markRead(notification.id);

    if (notification.type === 'ResourceShared') {
      let data: { resourceType?: string; resourceId?: string } = {};
      try { data = JSON.parse(notification.data ?? '{}'); } catch { /* ignore */ }

      if (data.resourceType === 'AiAssistant' && data.resourceId) {
        navigate(`/ai/assistants/${data.resourceId}`);
      } else {
        navigate(`${ROUTES.FILES}?view=shared`);
      }
    }
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm" className="relative">
          <Bell className="h-5 w-5" />
          {unreadCount > 0 && (
            <span className="absolute -top-0.5 -right-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-bold text-destructive-foreground">
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-80">
        <DropdownMenuLabel className="flex items-center justify-between">
          <span>{t('notifications.title')}</span>
          {unreadCount > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="h-auto p-0 text-xs text-primary hover:text-primary/80"
              onClick={(e) => {
                e.preventDefault();
                markAllRead();
              }}
            >
              <CheckCheck className="h-3 w-3 mr-1" />
              {t('notifications.markAllRead')}
            </Button>
          )}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {notifications.length === 0 ? (
          <div className="p-4 text-center text-sm text-muted-foreground">
            {t('notifications.noNotifications')}
          </div>
        ) : (
          <>
            {notifications.map((notification) => (
              <NotificationItem
                key={notification.id}
                notification={notification}
                onRead={n => handleNotificationClick(n)}
              />
            ))}
            <DropdownMenuSeparator />
            <DropdownMenuItem
              className="justify-center text-primary cursor-pointer"
              onClick={() => navigate(ROUTES.NOTIFICATIONS)}
            >
              {t('notifications.viewAll')}
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
