import { AlertCircle, CalendarClock, Clock, MinusCircle } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { TableCell, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { formatDate } from '@/utils/format';
import type { PendingTaskSummary } from '@/types/workflow.types';
import { deriveSlaState, type SlaPressure } from '../utils/sla-pressure';

interface InboxTaskRowProps {
  task: PendingTaskSummary;
  selected: boolean;
  bulkEligible: boolean;
  onToggleSelect: (id: string) => void;
  onAct: (task: PendingTaskSummary) => void;
}

// Inset left stripe — gives every row an at-a-glance severity colour without
// changing layout (uses box-shadow so the cell border-box stays intact).
const ROW_STRIPE: Record<SlaPressure, string> = {
  overdue: 'shadow-[inset_4px_0_0_hsl(var(--destructive))]',
  dueToday: 'shadow-[inset_4px_0_0_var(--state-warn-fg)]',
  onTrack: 'shadow-[inset_4px_0_0_hsl(142_71%_45%)]',
  noSla: 'shadow-[inset_4px_0_0_hsl(var(--border))]',
};

const PRESSURE_CHIP: Record<SlaPressure, string> = {
  overdue: 'border-destructive/40 bg-destructive/15 text-destructive font-semibold',
  dueToday: 'border-[var(--state-warn-border)] bg-[var(--state-warn-bg)] text-[var(--state-warn-fg)] font-semibold',
  onTrack: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400',
  noSla: 'border-border bg-muted/40 text-muted-foreground',
};

const PRESSURE_ICON: Record<SlaPressure, ReactNode> = {
  overdue: <AlertCircle className="h-3.5 w-3.5" />,
  dueToday: <CalendarClock className="h-3.5 w-3.5" />,
  onTrack: <Clock className="h-3.5 w-3.5" />,
  noSla: <MinusCircle className="h-3.5 w-3.5" />,
};

export function InboxTaskRow({
  task,
  selected,
  bulkEligible,
  onToggleSelect,
  onAct,
}: InboxTaskRowProps) {
  const { t } = useTranslation();
  const sla = deriveSlaState(task);
  const requestLabel = task.entityDisplayName ?? `${task.entityId.slice(0, 8)}...`;
  const pressureLabel = getPressureLabel(sla.pressure, sla.label, t);

  return (
    <TableRow data-pressure={sla.pressure}>
      <TableCell className={cn('w-[40px]', ROW_STRIPE[sla.pressure])}>
        <Checkbox
          checked={selected}
          disabled={!bulkEligible}
          title={!bulkEligible ? t('workflow.inbox.requiresForm') : undefined}
          onCheckedChange={() => onToggleSelect(task.taskId)}
          aria-label={t('workflow.inbox.select')}
        />
      </TableCell>
      <TableCell>
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="max-w-[280px] truncate font-medium text-foreground">
              {requestLabel}
            </span>
            <Badge
              variant="outline"
              className="border-[var(--active-border)] text-[var(--tinted-fg)]"
            >
              {task.definitionName}
            </Badge>
            {task.isDelegated && (
              <Badge variant="secondary">
                {t('workflow.delegation.badgeFrom', { name: task.delegatedFromDisplayName })}
              </Badge>
            )}
          </div>
          <div className="text-xs text-muted-foreground">{task.stepName}</div>
        </div>
      </TableCell>
      <TableCell className="w-[200px]">
        <span
          className={cn(
            'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs',
            PRESSURE_CHIP[sla.pressure],
          )}
        >
          {PRESSURE_ICON[sla.pressure]}
          {pressureLabel}
        </span>
      </TableCell>
      <TableCell className="w-[160px] text-xs text-muted-foreground">
        {formatDate(task.createdAt)}
      </TableCell>
      <TableCell className="w-[100px] text-end">
        <Button size="sm" onClick={() => onAct(task)}>
          {t('workflow.inbox.actOn')}
        </Button>
      </TableCell>
    </TableRow>
  );
}

function getPressureLabel(
  pressure: SlaPressure,
  relative: string | null,
  t: (key: string, options?: Record<string, unknown>) => string,
) {
  if (!relative) return t('workflow.inbox.sla.noSla');
  if (pressure === 'overdue') return t('workflow.inbox.sla.overdue', { relative });
  if (pressure === 'onTrack') return t('workflow.inbox.sla.onTrack');
  return t('workflow.inbox.sla.dueIn', { relative });
}
