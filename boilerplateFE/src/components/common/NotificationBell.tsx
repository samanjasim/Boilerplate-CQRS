import { Bell, CheckCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { formatDistanceToNow } from 'date-fns';
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
  const Icon = NOTIFICATION_ICONS[type] || NOTIFICATION_ICONS.default;
  return <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />;
}

function NotificationItem({
  notification,
  onRead,
}: {
  notification: Notification;
  onRead: (id: string) => void;
}) {
  const timeAgo = formatDistanceToNow(new Date(notification.createdAt), { addSuffix: true });

  return (
    <DropdownMenuItem
      className="flex items-start gap-3 p-3 cursor-pointer"
      onClick={() => onRead(notification.id)}
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
  const { data: unreadCount = 0 } = useUnreadCount();
  const { data: notificationsData } = useNotifications(
    { pageSize: 5 },
    { refetchInterval: ablyConnected ? false : 30000 }
  );
  const { mutate: markRead } = useMarkRead();
  const { mutate: markAllRead } = useMarkAllRead();

  const notifications = notificationsData?.data ?? [];

  const handleNotificationClick = (id: string) => {
    markRead(id);
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
                onRead={handleNotificationClick}
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
