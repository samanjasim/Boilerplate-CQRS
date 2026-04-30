import { formatDistanceToNowStrict } from 'date-fns';
import type { PendingTaskSummary } from '@/types/workflow.types';

export type SlaPressure = 'overdue' | 'dueToday' | 'onTrack' | 'noSla';

export interface SlaState {
  pressure: SlaPressure;
  label: string | null;
  fillPercent: number | null;
  priority: 'high' | 'medium' | 'low';
}

export function deriveSlaState(task: PendingTaskSummary, now: Date = new Date()): SlaState {
  if (!task.dueDate) {
    return { pressure: 'noSla', label: null, fillPercent: null, priority: 'low' };
  }

  const due = new Date(task.dueDate);
  const created = new Date(task.createdAt);
  const totalWindow = due.getTime() - created.getTime();
  const elapsed = now.getTime() - created.getTime();
  const fillPercent = totalWindow > 0
    ? Math.max(0, Math.min(100, (elapsed / totalWindow) * 100))
    : 100;

  const isOverdue = task.isOverdue ?? due.getTime() < now.getTime();
  const endOfToday = new Date(now);
  endOfToday.setHours(23, 59, 59, 999);
  const isDueToday = !isOverdue && due.getTime() <= endOfToday.getTime();

  if (isOverdue) {
    return {
      pressure: 'overdue',
      label: formatDistanceToNowStrict(due, { addSuffix: false }),
      fillPercent,
      priority: 'high',
    };
  }

  if (isDueToday) {
    return {
      pressure: 'dueToday',
      label: formatDistanceToNowStrict(due, { addSuffix: false }),
      fillPercent,
      priority: 'medium',
    };
  }

  return {
    pressure: 'onTrack',
    label: formatDistanceToNowStrict(due, { addSuffix: false }),
    fillPercent,
    priority: 'low',
  };
}
