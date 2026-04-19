import { useState } from 'react';
import { useParams, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, ConfirmDialog } from '@/components/common';
import { Slot } from '@/lib/extensions';
import { useBackNavigation, usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';
import { formatDateTime } from '@/utils/format';
import {
  useWorkflowHistory,
  useWorkflowDefinition,
  useCancelWorkflow,
  usePendingTasks,
} from '../api';
import { WorkflowStepTimeline } from '../components/WorkflowStepTimeline';
import { ApprovalDialog } from '../components/ApprovalDialog';
import type { WorkflowInstanceSummary, PendingTaskSummary } from '@/types/workflow.types';

const statusBadgeVariant: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Completed: 'secondary',
  Cancelled: 'destructive',
};

export default function WorkflowInstanceDetailPage() {
  const { t } = useTranslation();
  const { id: instanceId } = useParams<{ id: string }>();
  const location = useLocation();
  const { hasPermission } = usePermissions();

  useBackNavigation(ROUTES.WORKFLOWS.INSTANCES, t('workflow.instances.title'));

  const instance = (location.state as { instance?: WorkflowInstanceSummary })?.instance;

  const { data: history, isLoading: historyLoading } = useWorkflowHistory(instanceId!);
  const { data: definition } = useWorkflowDefinition(instance?.definitionId ?? '');
  const { mutate: cancelWorkflow, isPending: cancelling } = useCancelWorkflow();
  const { data: tasksData } = usePendingTasks();

  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [selectedTask, setSelectedTask] = useState<PendingTaskSummary | null>(null);

  const canCancel = hasPermission(PERMISSIONS.Workflows.Cancel);

  // Find pending task for this instance assigned to current user
  const pendingTasks: PendingTaskSummary[] = tasksData?.data ?? [];
  const myTask = pendingTasks.find(
    (task) => task.instanceId === instanceId,
  );

  if (!instance && historyLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!instance) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('workflow.detail.title')} />

        {historyLoading ? (
          <div className="flex justify-center py-12">
            <Spinner size="lg" />
          </div>
        ) : (
          <Card>
            <CardContent className="py-6">
              <p className="text-sm text-muted-foreground">
                {t('workflow.instances.empty')}
              </p>
            </CardContent>
          </Card>
        )}
      </div>
    );
  }

  const isActive = instance.status === 'Active';

  function handleCancel() {
    cancelWorkflow({ instanceId: instanceId! });
    setShowCancelDialog(false);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={instance.definitionName}
        actions={
          canCancel && isActive ? (
            <Button
              variant="outline"
              onClick={() => setShowCancelDialog(true)}
              disabled={cancelling}
            >
              <XCircle className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('workflow.detail.cancelWorkflow')}
            </Button>
          ) : undefined
        }
      />

      {/* Instance info */}
      <Card>
        <CardContent className="py-5">
          <div className="flex flex-wrap items-center gap-3">
            <Badge variant="secondary">{instance.entityType}</Badge>
            <span className="text-sm text-muted-foreground">
              {instance.entityId.substring(0, 8)}...
            </span>
            <Badge variant="outline">{instance.currentState}</Badge>
            <Badge variant={statusBadgeVariant[instance.status] ?? 'outline'}>
              {t(`workflow.status.${instance.status.toLowerCase()}`)}
            </Badge>
          </div>
          <p className="mt-3 text-sm text-muted-foreground">
            {t('workflow.instances.startedAt')}: {formatDateTime(instance.startedAt)}
          </p>
          {instance.startedByDisplayName && (
            <p className="text-sm text-muted-foreground">
              {t('workflow.instances.startedBy')}: {instance.startedByDisplayName}
            </p>
          )}
        </CardContent>
      </Card>

      {/* Pending action */}
      {myTask && (
        <Card>
          <CardContent className="py-5">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-sm font-semibold text-foreground">
                  {t('workflow.detail.pendingAction')}
                </h3>
                <p className="text-sm text-muted-foreground mt-1">
                  {myTask.stepName}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Button size="sm" onClick={() => setSelectedTask(myTask)}>
                  {t('workflow.inbox.approve')}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setSelectedTask(myTask)}
                >
                  {t('workflow.inbox.reject')}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Step timeline */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">
          {t('workflow.detail.stepHistory')}
        </h2>
        {definition?.states ? (
          <Card>
            <CardContent className="py-5">
              <WorkflowStepTimeline
                instanceId={instanceId!}
                currentState={instance.currentState}
                states={definition.states}
              />
            </CardContent>
          </Card>
        ) : historyLoading ? (
          <div className="flex justify-center py-6">
            <Spinner size="md" />
          </div>
        ) : (
          <Card>
            <CardContent className="py-5">
              {Array.isArray(history) || history?.data ? (
                <div className="space-y-2">
                  {(Array.isArray(history) ? history : history.data).map(
                    (record: { toState: string; action: string; actorDisplayName?: string; timestamp: string; comment?: string }, idx: number) => (
                      <div key={idx} className="flex items-start gap-3">
                        <div className="h-2.5 w-2.5 rounded-full bg-primary mt-1.5 shrink-0" />
                        <div>
                          <p className="text-sm text-foreground">
                            {record.toState} -- {record.action}
                          </p>
                          {record.actorDisplayName && (
                            <p className="text-xs text-muted-foreground">{record.actorDisplayName}</p>
                          )}
                          <p className="text-xs text-muted-foreground">
                            {formatDateTime(record.timestamp)}
                          </p>
                          {record.comment && (
                            <p className="text-xs text-muted-foreground italic mt-0.5">
                              &ldquo;{record.comment}&rdquo;
                            </p>
                          )}
                        </div>
                      </div>
                    ),
                  )}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">{t('workflow.instances.empty')}</p>
              )}
            </CardContent>
          </Card>
        )}
      </section>

      {/* Workflow status slot */}
      <Slot
        id="entity-detail-workflow"
        props={{ entityType: instance.entityType, entityId: instance.entityId }}
      />

      {/* Comments & Activity slot */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">
          {t('workflow.detail.comments')}
        </h2>
        <Slot
          id="entity-detail-timeline"
          props={{ entityType: instance.entityType, entityId: instance.entityId }}
        />
      </section>

      {/* Cancel dialog */}
      <ConfirmDialog
        isOpen={showCancelDialog}
        onClose={() => setShowCancelDialog(false)}
        title={t('workflow.detail.cancelWorkflow')}
        description={t('workflow.instances.cancelConfirm')}
        onConfirm={handleCancel}
        confirmLabel={t('workflow.detail.cancelWorkflow')}
        variant="danger"
      />

      {/* Approval dialog */}
      {selectedTask && (
        <ApprovalDialog
          taskId={selectedTask.taskId}
          definitionName={instance.definitionName}
          entityType={instance.entityType}
          entityId={instance.entityId}
          actions={['Approve', 'Reject', 'ReturnForRevision']}
          open={!!selectedTask}
          onOpenChange={(open) => { if (!open) setSelectedTask(null); }}
        />
      )}
    </div>
  );
}
