import type { Notification } from '@/types';

export type GroupKey =
  | 'today'
  | 'yesterday'
  | 'earlierThisWeek'
  | 'earlierThisMonth'
  | 'older';

const ORDER: GroupKey[] = [
  'today',
  'yesterday',
  'earlierThisWeek',
  'earlierThisMonth',
  'older',
];

export interface NotificationGroup {
  key: GroupKey;
  items: Notification[];
}

function startOfDay(date: Date): Date {
  const copy = new Date(date);
  copy.setHours(0, 0, 0, 0);
  return copy;
}

function classify(createdAt: string, now: Date): GroupKey {
  const created = new Date(createdAt);
  const today = startOfDay(now);
  const yesterday = startOfDay(new Date(today.getTime() - 86400000));

  if (created >= today) return 'today';
  if (created >= yesterday) return 'yesterday';

  const dayOfWeek = (today.getDay() + 6) % 7;
  const weekStart = startOfDay(new Date(today.getTime() - dayOfWeek * 86400000));
  if (created >= weekStart) return 'earlierThisWeek';

  const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
  if (created >= monthStart) return 'earlierThisMonth';

  return 'older';
}

export function groupNotificationsByDate(
  notifications: Notification[],
  now: Date = new Date()
): NotificationGroup[] {
  const buckets = new Map<GroupKey, Notification[]>();

  for (const notification of notifications) {
    const key = classify(notification.createdAt, now);
    if (!buckets.has(key)) buckets.set(key, []);
    buckets.get(key)!.push(notification);
  }

  return ORDER
    .filter((key) => buckets.has(key))
    .map((key) => ({ key, items: buckets.get(key)! }));
}
