import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { useWorkflowHistory } from '../api';
import { formatDateTime } from '@/utils/format';
import type { WorkflowStateConfig, WorkflowStepRecord } from '@/types/workflow.types';

interface WorkflowStepTimelineProps {
  instanceId: string;
  currentState: string;
  states: WorkflowStateConfig[];
}

type StepStatus = 'completed' | 'current' | 'future';

function getStepStatus(stateName: string, currentState: string, states: WorkflowStateConfig[]): StepStatus {
  const currentIndex = states.findIndex((s) => s.name === currentState);
  const stateIndex = states.findIndex((s) => s.name === stateName);
  if (stateIndex < currentIndex) return 'completed';
  if (stateIndex === currentIndex) return 'current';
  return 'future';
}

const dotStyles: Record<StepStatus, string> = {
  completed: 'bg-foreground/50',
  current: 'bg-primary',
  future: 'bg-muted-foreground/30',
};

const lineStyles: Record<StepStatus, string> = {
  completed: 'bg-foreground/50',
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

export function WorkflowStepTimeline({ instanceId, currentState, states }: WorkflowStepTimelineProps) {
  const { t } = useTranslation();
  const { data: history } = useWorkflowHistory(instanceId);

  const records: WorkflowStepRecord[] = Array.isArray(history) ? history : history?.data ?? [];

  const getRecordForState = (stateName: string) =>
    records.find((r) => r.toState === stateName);

  if (!states || states.length === 0) return null;

  return (
    <div className="space-y-0">
      {states.map((state, index) => {
        const status = getStepStatus(state.name, currentState, states);
        const record = getRecordForState(state.name);
        const isLast = index === states.length - 1;

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
    </div>
  );
}
