import { useState } from 'react';
import { useParams, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { XCircle, Send, AlertCircle, ShieldOff } from 'lucide-react';
import { AxiosError } from 'axios';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, ConfirmDialog, EmptyState } from '@/components/common';
import { Slot } from '@/lib/extensions';
import { usePermissions } from '@/hooks';
import { useAuthStore, selectUser } from '@/stores';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';
import { formatDateTime } from '@/utils/format';
import {
  useWorkflowHistory,
  useWorkflowInstanceById,
  useWorkflowDefinition,
  useCancelWorkflow,
  usePendingTasks,
  useTransitionWorkflow,
} from '../api';
import { WorkflowStepTimeline } from '../components/WorkflowStepTimeline';
import { ApprovalDialog } from '../components/ApprovalDialog';
import { InstanceMetadataRail } from '../components/InstanceMetadataRail';
import type { WorkflowInstanceSummary, PendingTaskSummary } from '@/types/workflow.types';

export default function WorkflowInstanceDetailPage() {
  const { t } = useTranslation();
  const { id: instanceId } = useParams<{ id: string }>();
  const location = useLocation();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);

  const stateInstance = (location.state as { instance?: WorkflowInstanceSummary })?.instance;
  const { data: fetchedInstance } = useWorkflowInstanceById(stateInstance ? undefined : instanceId);
  const instance = stateInstance ?? fetchedInstance;

  const { data: history, isLoading: historyLoading, error: historyError } = useWorkflowHistory(instanceId!);
  const { data: definition } = useWorkflowDefinition(instance?.definitionId ?? '');
  const { mutate: cancelWorkflow, isPending: cancelling } = useCancelWorkflow();
  const { mutate: transitionWorkflow, isPending: transitioning } = useTransitionWorkflow();
  const { data: tasksData } = usePendingTasks();

  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [selectedTask, setSelectedTask] = useState<PendingTaskSummary | null>(null);

  const canCancel = hasPermission(PERMISSIONS.Workflows.Cancel);

  // Find pending task for this instance assigned to current user
  const pendingTasks: PendingTaskSummary[] = tasksData?.data ?? [];
  const myTask = pendingTasks.find((task) => task.instanceId === instanceId);
  const isSuperAdmin = !user?.tenantId;

  // Check if the current user can resubmit
  const canResubmit =
    instance?.canResubmit &&
    instance?.status === 'Active' &&
    instance?.startedByUserId === user?.id;

  if (!instance && historyLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  // Handle access-denied (403) or not-found (404) from the history endpoint
  const historyStatusCode = historyError instanceof AxiosError ? historyError.response?.status : undefined;
  if (!instance && historyError) {
    const isAccessDenied = historyStatusCode === 403;
    return (
      <div className="space-y-6">
        <PageHeader
          title={t('workflow.detail.title')}
          breadcrumbs={[
            { to: ROUTES.WORKFLOWS.INSTANCES, label: t('workflow.instances.title') },
            { label: t('workflow.detail.title') },
          ]}
        />
        <Card>
          <CardContent className="py-6">
            <EmptyState
              icon={isAccessDenied ? ShieldOff : XCircle}
              title={isAccessDenied ? t('workflow.instances.accessDenied') : t('workflow.instances.empty')}
              description={isAccessDenied ? t('workflow.instances.accessDeniedDesc') : undefined}
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!instance) {
    return (
      <div className="space-y-6">
        <PageHeader
          title={t('workflow.detail.title')}
          breadcrumbs={[
            { to: ROUTES.WORKFLOWS.INSTANCES, label: t('workflow.instances.title') },
            { label: t('common.loading') },
          ]}
        />

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

  function handleResubmit() {
    transitionWorkflow({ instanceId: instanceId!, trigger: 'Submit' });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={instance.definitionName}
        breadcrumbs={[
          { to: ROUTES.WORKFLOWS.INSTANCES, label: t('workflow.instances.title') },
          { label: instance.entityDisplayName ?? instance.entityId.slice(0, 8) },
        ]}
      />

      {/* Resubmit notice */}
      {canResubmit && (
        <Card>
          <CardContent className="py-5">
            <div className="flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-amber-500 shrink-0 mt-0.5" />
              <div>
                <h3 className="text-sm font-semibold text-foreground">
                  {t('workflow.detail.returnedForRevision')}
                </h3>
                <p className="text-sm text-muted-foreground mt-1">
                  {t('workflow.detail.returnedForRevisionDesc')}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        <InstanceMetadataRail
          instance={instance}
          myTask={myTask ?? null}
          isSuperAdmin={isSuperAdmin}
          onAct={setSelectedTask}
          className="lg:order-2"
          actions={
            <>
              {canResubmit && (
                <Button
                  variant="default"
                  size="sm"
                  onClick={handleResubmit}
                  disabled={transitioning}
                >
                  <Send className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  {t('workflow.detail.resubmit')}
                </Button>
              )}
              {canCancel && isActive && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setShowCancelDialog(true)}
                  disabled={cancelling}
                >
                  <XCircle className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  {t('workflow.detail.cancelWorkflow')}
                </Button>
              )}
            </>
          }
        />

        <div className="min-w-0 space-y-6 lg:order-1">
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
                    instanceStatus={instance.status}
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
                  {history && history.length > 0 ? (
                    <div className="space-y-2">
                      {history.map(
                        (record, idx: number) => (
                          <div key={idx} className="flex items-start gap-3">
                            <div className="mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full bg-primary" />
                            <div className="min-w-0 flex-1">
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
                                <p className="mt-0.5 text-xs italic text-muted-foreground">
                                  &ldquo;{record.comment}&rdquo;
                                </p>
                              )}
                              {record.formData && typeof record.formData === 'object' && Object.keys(record.formData).length > 0 && (
                                <div className="mt-2 rounded-lg border border-border bg-muted/30 p-2">
                                  <p className="mb-1 text-xs font-medium text-muted-foreground">
                                    {t('workflow.forms.submittedData')}
                                  </p>
                                  <div className="space-y-0.5">
                                    {Object.entries(record.formData)
                                      .filter(([, v]) => v !== null && v !== undefined && v !== '')
                                      .map(([key, value]) => (
                                        <div key={key} className="flex items-baseline gap-2 text-xs">
                                          <span className="shrink-0 font-medium text-muted-foreground">{key}:</span>
                                          <span className="text-foreground">{String(value)}</span>
                                        </div>
                                      ))}
                                  </div>
                                </div>
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

          {/* Comments & Activity — uses WorkflowInstance as the entity type
              so all approval comments + activity entries appear on this page */}
          <section className="space-y-3">
            <h2 className="text-base font-semibold text-foreground">
              {t('workflow.detail.comments')}
            </h2>
            <Slot
              id="entity-detail-timeline"
              props={{ entityType: 'WorkflowInstance', entityId: instance.instanceId }}
            />
          </section>
        </div>
      </div>

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
          actions={selectedTask.availableActions ?? ['Approve', 'Reject', 'ReturnForRevision']}
          formFields={selectedTask.formFields}
          open={!!selectedTask}
          onOpenChange={(open) => { if (!open) setSelectedTask(null); }}
        />
      )}
    </div>
  );
}
