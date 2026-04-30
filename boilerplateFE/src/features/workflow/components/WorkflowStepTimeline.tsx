import { GitBranch } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { useWorkflowHistory } from '../api';
import { cn } from '@/lib/utils';
import { formatDateTime } from '@/utils/format';
import type { WorkflowStateConfig, WorkflowStepRecord } from '@/types/workflow.types';

interface WorkflowStepTimelineProps {
  instanceId: string;
  currentState: string;
  states: WorkflowStateConfig[];
  /** Instance.status: Active | Completed | Cancelled. When Completed/Cancelled,
   *  the terminal state is rendered as 'completed' (not 'current'). */
  instanceStatus?: string;
}

type StepStatus = 'completed' | 'current' | 'future';

function getStepStatus(
  stateName: string,
  currentState: string,
  history: WorkflowStepRecord[],
  instanceStatus?: string,
): StepStatus {
  if (stateName === currentState) {
    // Workflow ended on this state — visualize as completed, not current.
    if (instanceStatus === 'Completed' || instanceStatus === 'Cancelled') {
      return 'completed';
    }
    return 'current';
  }
  return history.some((r) => r.toState === stateName) ? 'completed' : 'future';
}

const dotStyles: Record<StepStatus, string> = {
  completed: 'bg-muted-foreground',
  current: 'bg-primary shadow-[var(--glow-primary-sm)]',
  future: 'bg-muted-foreground/30',
};

const lineStyles: Record<StepStatus, string> = {
  completed: 'bg-muted-foreground',
  current: 'bg-primary',
  future: 'bg-border',
};

function FormDataDisplay({ formData }: { formData: Record<string, unknown> }) {
  const { t } = useTranslation();
  const entries = Object.entries(formData).filter(([, v]) => v !== null && v !== undefined && v !== '');
  if (entries.length === 0) return null;

  return (
    <div className="mt-2 rounded-lg border border-border bg-muted/30 p-2">
      <p className="text-xs font-medium text-muted-foreground mb-1">
        {t('workflow.forms.submittedData')}
      </p>
      <div className="space-y-0.5">
        {entries.map(([key, value]) => (
          <div key={key} className="flex items-baseline gap-2 text-xs">
            <span className="text-muted-foreground font-medium shrink-0">{key}:</span>
            <span className="text-foreground">{String(value)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function isTerminalType(type?: string) {
  return type === 'Terminal' || type === 'Final';
}

const POSITIVE_TERMINAL_NAME =
  /^(approved?|complet(ed)?|passed?|resolv(ed)?|granted|success(ful)?|done|published?|active)$/i;
const NEGATIVE_TERMINAL_NAME =
  /^(rejected?|cancell?ed|fail(ed)?|denied|abort(ed)?|error|archived?|expired)$/i;

function isPositiveTerminal(name: string) {
  return POSITIVE_TERMINAL_NAME.test(name);
}
function isNegativeTerminal(name: string) {
  return NEGATIVE_TERMINAL_NAME.test(name);
}

export function WorkflowStepTimeline({
  instanceId,
  currentState,
  states,
  instanceStatus,
}: WorkflowStepTimelineProps) {
  const { t } = useTranslation();
  const { data: records = [], isLoading } = useWorkflowHistory(instanceId);

  const getRecordForState = (stateName: string) =>
    records.find((r) => r.toState === stateName);

  if (!states || states.length === 0) return null;

  // Guard against the flash where history hasn't loaded yet: without it,
  // every non-current state briefly renders as 'future' before history arrives.
  if (isLoading) {
    return (
      <div className="flex justify-center py-6">
        <Spinner size="md" />
      </div>
    );
  }

  // Once a workflow reaches a terminal, hide the untraversed terminal siblings —
  // Approved/Rejected don't both belong on a history view once the decision
  // was made. While the workflow is still active, render the unreached
  // terminals as a single horizontal "Possible outcomes" group at the bottom
  // so users see them as parallel branches, not as sequential steps.
  const terminals = states.filter((s) => isTerminalType(s.type));
  const reachedTerminal =
    terminals.find((s) => s.name === currentState
        && (instanceStatus === 'Completed' || instanceStatus === 'Cancelled'))
    ?? terminals.find((s) => records.some((r) => r.toState === s.name));

  const sequentialStates = reachedTerminal
    ? states.filter((s) => !isTerminalType(s.type) || s.name === reachedTerminal.name)
    : states.filter((s) => !isTerminalType(s.type));

  // Show unreached terminals as a horizontal fork group only while the workflow
  // is still active (no terminal has been reached yet).
  const futureTerminals = reachedTerminal
    ? []
    : terminals;

  return (
    <div className="space-y-0">
      {sequentialStates.map((state, index) => {
        const status = getStepStatus(state.name, currentState, records, instanceStatus);
        const record = getRecordForState(state.name);
        const isLast = index === sequentialStates.length - 1 && futureTerminals.length === 0;

        // Check for parallel info in metadata
        const parallelTotal = record?.metadata?.parallelTotal as number | undefined;
        const parallelCompleted = record?.metadata?.parallelCompleted as number | undefined;
        const isOverdue = record?.metadata?.isOverdue as boolean | undefined;

        return (
          <div key={state.name} className="flex gap-3">
            {/* Timeline dot + line */}
            <div className="flex flex-col items-center">
              <div className={`h-3 w-3 rounded-full shrink-0 mt-1.5 ${isOverdue ? 'bg-destructive' : dotStyles[status]}`} />
              {!isLast && (
                <div className={`w-0.5 flex-1 min-h-[24px] ${lineStyles[status]}`} />
              )}
            </div>

            {/* Step content */}
            <div className="pb-4 min-w-0 flex-1">
              <div className="flex items-center gap-2 flex-wrap">
                <p className={`text-sm font-medium ${status === 'future' ? 'text-muted-foreground' : 'text-foreground'}`}>
                  {state.displayName || state.name}
                </p>
                {isOverdue && (
                  <Badge variant="destructive" className="text-[10px] px-1.5 py-0">
                    {t('workflow.sla.overdue')}
                  </Badge>
                )}
                {parallelTotal != null && parallelTotal > 1 && (
                  <span className="text-xs text-muted-foreground">
                    {t('workflow.parallel.progress', {
                      completed: parallelCompleted ?? 0,
                      total: parallelTotal,
                    })}
                  </span>
                )}
              </div>
              {record && (
                <div className="mt-1 space-y-0.5">
                  {record.actorDisplayName && (
                    <p className="text-xs text-muted-foreground">
                      {record.actorDisplayName} — {record.action}
                    </p>
                  )}
                  <p className="text-xs text-muted-foreground">
                    {formatDateTime(record.timestamp)}
                  </p>
                  {record.comment && (
                    <p className="text-xs text-muted-foreground italic mt-1">
                      &ldquo;{record.comment}&rdquo;
                    </p>
                  )}
                  {record.formData && Object.keys(record.formData).length > 0 && (
                    <FormDataDisplay formData={record.formData} />
                  )}
                </div>
              )}
              {status === 'current' && !record && (
                <p className="text-xs text-primary mt-0.5">{t('workflow.status.pending')}</p>
              )}
            </div>
          </div>
        );
      })}
      {futureTerminals.length > 0 && (
        <div className="flex gap-3">
          <div className="flex flex-col items-center">
            <GitBranch className="h-3 w-3 mt-1 text-muted-foreground/60 shrink-0" />
          </div>
          <div className="pb-1 min-w-0 flex-1">
            <p className="text-xs uppercase tracking-wide text-muted-foreground/70 mb-2">
              {t('workflow.detail.possibleOutcomes')}
            </p>
            <div className="flex flex-wrap gap-2">
              {futureTerminals.map((terminal) => (
                <span
                  key={terminal.name}
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-xs',
                    isPositiveTerminal(terminal.name) && 'border-emerald-500/40 bg-emerald-50 text-emerald-700 dark:bg-emerald-950/20 dark:text-emerald-400',
                    isNegativeTerminal(terminal.name) && 'border-destructive/40 bg-destructive/10 text-destructive',
                    !isPositiveTerminal(terminal.name) && !isNegativeTerminal(terminal.name) && 'border-border bg-muted/40 text-muted-foreground',
                  )}
                >
                  <span
                    className={cn(
                      'h-1.5 w-1.5 rounded-full',
                      isPositiveTerminal(terminal.name) && 'bg-emerald-500',
                      isNegativeTerminal(terminal.name) && 'bg-destructive',
                      !isPositiveTerminal(terminal.name) && !isNegativeTerminal(terminal.name) && 'bg-muted-foreground/40',
                    )}
                  />
                  {terminal.displayName || terminal.name}
                </span>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
