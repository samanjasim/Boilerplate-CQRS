import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inbox, Users, X, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { usePendingTasks, useActiveDelegation, useCancelDelegation, useBatchExecuteTasks } from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ApprovalDialog } from '../components/ApprovalDialog';
import { DelegationDialog } from '../components/DelegationDialog';
import { NewRequestDialog } from '../components/NewRequestDialog';
import { BulkActionBar } from '../components/BulkActionBar';
import { BulkConfirmDialog } from '../components/BulkConfirmDialog';
import { BulkResultDialog } from '../components/BulkResultDialog';
import { InboxStatusHero } from '../components/InboxStatusHero';
import { InboxTaskRow } from '../components/InboxTaskRow';
import { formatDate } from '@/utils/format';
import type { PendingTaskSummary, BatchExecuteResult } from '@/types/workflow.types';

type BulkAction = 'Approve' | 'Reject' | 'ReturnForRevision';

export default function WorkflowInboxPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canStart = hasPermission(PERMISSIONS.Workflows.Start);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [selectedTask, setSelectedTask] = useState<PendingTaskSummary | null>(null);
  const [delegationOpen, setDelegationOpen] = useState(false);
  const [newRequestOpen, setNewRequestOpen] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [pendingBulkAction, setPendingBulkAction] = useState<BulkAction | null>(null);
  const [bulkResult, setBulkResult] = useState<BatchExecuteResult | null>(null);
  const [bulkResultLabels, setBulkResultLabels] = useState<Record<string, string>>({});
  const [lastBulkPayload, setLastBulkPayload] = useState<
    { action: BulkAction; comment?: string } | null
  >(null);

  const { data, isLoading } = usePendingTasks({ page, pageSize });
  const { data: activeDelegation } = useActiveDelegation();
  const { mutate: cancelDelegation, isPending: cancellingDelegation } = useCancelDelegation();
  const { mutate: batchExecute, isPending: isBulkPending } = useBatchExecuteTasks();

  const tasks: PendingTaskSummary[] = data?.data ?? [];
  const pagination = data?.pagination;
  const hasDelegation = !!activeDelegation?.isActive;

  const requiresForm = (task: PendingTaskSummary) =>
    !!task.formFields?.some((f) => f.required);

  const handlePageChange = (next: number) => {
    setSelectedIds(new Set());
    setPage(next);
  };

  const handlePageSizeChange = (next: number) => {
    setSelectedIds(new Set());
    setPageSize(next);
  };

  const bulkEligibleIds = tasks.filter((t) => !requiresForm(t)).map((t) => t.taskId);
  const allSelected =
    bulkEligibleIds.length > 0 && bulkEligibleIds.every((id) => selectedIds.has(id));

  const toggleOne = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    setSelectedIds((prev) => {
      if (allSelected) {
        const next = new Set(prev);
        bulkEligibleIds.forEach((id) => next.delete(id));
        return next;
      }
      return new Set([...prev, ...bulkEligibleIds]);
    });
  };

  const clearSelection = () => setSelectedIds(new Set());

  const buildTaskLabels = (ids: Iterable<string>): Record<string, string> => {
    const labels: Record<string, string> = {};
    const byId = new Map(tasks.map((t) => [t.taskId, t]));
    for (const id of ids) {
      const task = byId.get(id);
      labels[id] = task
        ? `${task.entityType} · ${task.entityDisplayName ?? task.entityId.substring(0, 8) + '...'}`
        : id.substring(0, 8) + '...';
    }
    return labels;
  };

  const confirmBulk = (comment: string | undefined) => {
    if (!pendingBulkAction || selectedIds.size === 0) return;
    const taskIds = Array.from(selectedIds);
    const labels = buildTaskLabels(taskIds);
    const action = pendingBulkAction;
    batchExecute(
      { taskIds, action, comment },
      {
        onSuccess: (result) => {
          setBulkResultLabels(labels);
          setBulkResult(result);
          setLastBulkPayload({ action, comment });
          clearSelection();
        },
        onSettled: () => setPendingBulkAction(null),
      },
    );
  };

  const retryFailed = () => {
    if (!bulkResult || !lastBulkPayload) return;
    const failedIds = bulkResult.items
      .filter((item) => item.status === 'Failed')
      .map((item) => item.taskId);
    if (failedIds.length === 0) return;
    batchExecute(
      { taskIds: failedIds, action: lastBulkPayload.action, comment: lastBulkPayload.comment },
      {
        onSuccess: (retryResult) => {
          const retryById = new Map(retryResult.items.map((i) => [i.taskId, i]));
          const mergedItems = bulkResult.items.map((orig) => retryById.get(orig.taskId) ?? orig);
          const counts = mergedItems.reduce(
            (acc, item) => {
              if (item.status === 'Succeeded') acc.succeeded += 1;
              else if (item.status === 'Failed') acc.failed += 1;
              else acc.skipped += 1;
              return acc;
            },
            { succeeded: 0, failed: 0, skipped: 0 },
          );
          setBulkResult({ ...counts, items: mergedItems });
        },
      },
    );
  };

  return (
    <div className={`space-y-6 ${selectedIds.size > 0 ? 'pb-28' : ''}`}>
      <PageHeader
        title={t('workflow.inbox.title')}
        actions={
          <div className="flex items-center gap-2">
            {canStart && (
              <Button onClick={() => setNewRequestOpen(true)}>
                <Plus className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                {t('workflow.newRequest.title')}
              </Button>
            )}
            <Button variant="outline" onClick={() => setDelegationOpen(true)}>
              <Users className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('workflow.delegation.title')}
            </Button>
          </div>
        }
      />

      <InboxStatusHero />

      {/* Active delegation banner */}
      {hasDelegation && (
        <div className="flex items-center justify-between rounded-xl border border-primary/20 bg-primary/5 px-4 py-3">
          <p className="text-sm text-foreground">
            {t('workflow.delegation.banner', {
              name: activeDelegation!.toDisplayName ?? activeDelegation!.toUserId,
              date: formatDate(activeDelegation!.endDate),
            })}
          </p>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => cancelDelegation(activeDelegation!.id)}
            disabled={cancellingDelegation}
          >
            <X className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
            {t('workflow.delegation.cancel')}
          </Button>
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner size="lg" />
        </div>
      ) : tasks.length === 0 ? (
        <EmptyState
          icon={Inbox}
          title={t('workflow.inbox.empty')}
          description={t('workflow.inbox.emptyDesc')}
        />
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[40px]">
                  <Checkbox
                    aria-label={t('workflow.inbox.selectAll')}
                    checked={allSelected}
                    disabled={bulkEligibleIds.length === 0}
                    onCheckedChange={toggleAll}
                  />
                </TableHead>
                <TableHead>{t('workflow.inbox.request')}</TableHead>
                <TableHead className="w-[180px]">{t('workflow.inbox.sla.header')}</TableHead>
                <TableHead className="w-[160px]">{t('workflow.inbox.raised')}</TableHead>
                <TableHead className="w-[100px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {tasks.map((task) => (
                <InboxTaskRow
                  key={task.taskId}
                  task={task}
                  selected={selectedIds.has(task.taskId)}
                  bulkEligible={!requiresForm(task)}
                  onToggleSelect={toggleOne}
                  onAct={setSelectedTask}
                />
              ))}
            </TableBody>
          </Table>

          {pagination && (
            <Pagination
              pagination={pagination}
              onPageChange={handlePageChange}
              onPageSizeChange={handlePageSizeChange}
            />
          )}
        </>
      )}

      {selectedIds.size > 0 && (
        <BulkActionBar
          selectedCount={selectedIds.size}
          isPending={isBulkPending}
          onApprove={() => setPendingBulkAction('Approve')}
          onReject={() => setPendingBulkAction('Reject')}
          onReturn={() => setPendingBulkAction('ReturnForRevision')}
          onClear={clearSelection}
        />
      )}

      <BulkConfirmDialog
        action={pendingBulkAction}
        count={selectedIds.size}
        isPending={isBulkPending}
        onSubmit={confirmBulk}
        onCancel={() => setPendingBulkAction(null)}
      />

      <BulkResultDialog
        result={bulkResult}
        taskLabels={bulkResultLabels}
        isRetrying={isBulkPending}
        onRetry={retryFailed}
        onClose={() => {
          setBulkResult(null);
          setBulkResultLabels({});
          setLastBulkPayload(null);
        }}
      />

      {selectedTask && (
        <ApprovalDialog
          taskId={selectedTask.taskId}
          definitionName={selectedTask.definitionName}
          entityType={selectedTask.entityType}
          entityId={selectedTask.entityId}
          actions={selectedTask.availableActions ?? ['Approve', 'Reject', 'ReturnForRevision']}
          formFields={selectedTask.formFields}
          open={!!selectedTask}
          onOpenChange={(open) => { if (!open) setSelectedTask(null); }}
        />
      )}

      <DelegationDialog
        open={delegationOpen}
        onOpenChange={setDelegationOpen}
      />

      <NewRequestDialog open={newRequestOpen} onOpenChange={setNewRequestOpen} />
    </div>
  );
}
