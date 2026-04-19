import { useTranslation } from 'react-i18next';
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
  completed: 'bg-green-500',
  current: 'bg-orange-500',
  future: 'bg-muted-foreground/30',
};

const lineStyles: Record<StepStatus, string> = {
  completed: 'bg-green-500',
  current: 'bg-orange-500',
  future: 'bg-border',
};

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

        return (
          <div key={state.name} className="flex gap-3">
            {/* Timeline dot + line */}
            <div className="flex flex-col items-center">
              <div className={`h-3 w-3 rounded-full shrink-0 mt-1.5 ${dotStyles[status]}`} />
              {!isLast && (
                <div className={`w-0.5 flex-1 min-h-[24px] ${lineStyles[status]}`} />
              )}
            </div>

            {/* Step content */}
            <div className="pb-4 min-w-0 flex-1">
              <p className={`text-sm font-medium ${status === 'future' ? 'text-muted-foreground' : 'text-foreground'}`}>
                {state.displayName || state.name}
              </p>
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
                </div>
              )}
              {status === 'current' && !record && (
                <p className="text-xs text-orange-600 mt-0.5">{t('workflow.status.pending')}</p>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
