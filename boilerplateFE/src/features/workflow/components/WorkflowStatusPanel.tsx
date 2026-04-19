import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { GitBranch } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { useWorkflowStatus, useWorkflowDefinition, usePendingTasks } from '../api';
import { WorkflowStepTimeline } from './WorkflowStepTimeline';
import { ApprovalDialog } from './ApprovalDialog';
import type { PendingTaskSummary } from '@/types/workflow.types';

interface WorkflowStatusPanelProps {
  entityType: string;
  entityId: string;
}

export function WorkflowStatusPanel({ entityType, entityId }: WorkflowStatusPanelProps) {
  const { t } = useTranslation();
  const { data: status } = useWorkflowStatus(entityType, entityId);
  const { data: definition } = useWorkflowDefinition(status?.definitionId ?? '');
  const { data: tasksData } = usePendingTasks({ page: 1, pageSize: 100 });
  const [selectedTask, setSelectedTask] = useState<PendingTaskSummary | null>(null);

  if (!status) return null;

  const pendingTasks: PendingTaskSummary[] = tasksData?.data ?? [];
  const myTask = pendingTasks.find(
    (task) => task.entityType === entityType && task.entityId === entityId,
  );

  const states = definition?.states ?? [];

  return (
    <Card>
      <CardContent className="py-5">
        <div className="flex items-center gap-2 mb-4">
          <GitBranch className="h-5 w-5 text-primary" />
          <h3 className="text-base font-semibold text-foreground">
            {status.definitionName}
          </h3>
        </div>

        <div className="flex items-center gap-2 mb-4">
          <Badge variant={STATUS_BADGE_VARIANT[status.status] ?? 'secondary'}>
            {String(t(`workflow.status.${status.status.toLowerCase()}`, { defaultValue: status.status }))}
          </Badge>
          <span className="text-sm text-muted-foreground">{status.currentState}</span>
        </div>

        {myTask && (
          <div className="mb-4">
            <Button size="sm" onClick={() => setSelectedTask(myTask)}>
              {t('workflow.inbox.approve')}
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="ms-2"
              onClick={() => setSelectedTask(myTask)}
            >
              {t('workflow.inbox.reject')}
            </Button>
          </div>
        )}

        {states.length > 0 && (
          <WorkflowStepTimeline
            instanceId={status.instanceId}
            currentState={status.currentState}
            states={states}
          />
        )}

        {selectedTask && (
          <ApprovalDialog
            taskId={selectedTask.taskId}
            definitionName={selectedTask.definitionName}
            entityType={selectedTask.entityType}
            entityId={selectedTask.entityId}
            actions={selectedTask.availableActions ?? ['Approve', 'Reject', 'ReturnForRevision']}
            open={!!selectedTask}
            onOpenChange={(open) => { if (!open) setSelectedTask(null); }}
          />
        )}
      </CardContent>
    </Card>
  );
}
