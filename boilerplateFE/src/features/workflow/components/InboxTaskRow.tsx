import { AlertCircle, Clock } from 'lucide-react';
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

const PRIORITY_DOT: Record<SlaPressure, string> = {
  overdue: 'bg-destructive',
  dueToday: 'bg-[var(--state-warn-fg)]',
  onTrack: 'bg-muted-foreground/40',
  noSla: 'bg-muted-foreground/20',
};

const PRESSURE_CHIP: Record<SlaPressure, string> = {
  overdue: 'border-destructive/30 bg-destructive/10 text-destructive',
  dueToday: 'border-[var(--state-warn-border)] bg-[var(--state-warn-bg)] text-[var(--state-warn-fg)]',
  onTrack: 'border-border bg-muted/50 text-muted-foreground',
  noSla: 'border-border bg-muted/30 text-muted-foreground/70',
};

const SLA_BAR_FILL: Record<SlaPressure, string> = {
  overdue: 'bg-destructive',
  dueToday: 'bg-[var(--state-warn-fg)]',
  onTrack: 'bg-emerald-500',
  noSla: 'bg-muted-foreground/20',
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
      <TableCell className="w-[40px]">
        <Checkbox
          checked={selected}
          disabled={!bulkEligible}
          title={!bulkEligible ? t('workflow.inbox.requiresForm') : undefined}
          onCheckedChange={() => onToggleSelect(task.taskId)}
          aria-label={t('workflow.inbox.select')}
        />
      </TableCell>
      <TableCell>
        <div className="flex items-start gap-2">
          <span
            className={cn('mt-1.5 h-2 w-2 shrink-0 rounded-full', PRIORITY_DOT[sla.pressure])}
            aria-hidden
          />
          <div className="min-w-0 flex-1 space-y-1.5">
            <div className="flex flex-wrap items-center gap-2">
              <span className="max-w-[260px] truncate font-medium text-foreground">
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
            {sla.fillPercent !== null && (
              <div className="h-1 w-full overflow-hidden rounded-full bg-muted">
                <div
                  className={cn('h-full transition-all', SLA_BAR_FILL[sla.pressure])}
                  style={{ width: `${sla.fillPercent}%` }}
                />
              </div>
            )}
          </div>
        </div>
      </TableCell>
      <TableCell className="w-[180px]">
        <span
          className={cn(
            'inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs',
            PRESSURE_CHIP[sla.pressure],
          )}
        >
          {sla.pressure === 'overdue' ? (
            <AlertCircle className="h-3 w-3" />
          ) : (
            <Clock className="h-3 w-3" />
          )}
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
