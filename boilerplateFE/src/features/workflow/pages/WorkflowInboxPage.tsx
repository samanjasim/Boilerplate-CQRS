import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inbox, Users, X, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
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

  const { data, isLoading } = usePendingTasks({ page, pageSize });
  const { data: activeDelegation } = useActiveDelegation();
  const { mutate: cancelDelegation, isPending: cancellingDelegation } = useCancelDelegation();
  const { mutate: batchExecute, isPending: isBulkPending } = useBatchExecuteTasks();

  const tasks: PendingTaskSummary[] = data?.data ?? [];
  const pagination = data?.pagination;
  const hasDelegation = !!activeDelegation?.isActive;

  useEffect(() => {
    setSelectedIds(new Set());
  }, [page, pageSize]);

  const allVisibleIds = tasks.map((x) => x.taskId);
  const allSelected =
    allVisibleIds.length > 0 && allVisibleIds.every((id) => selectedIds.has(id));

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
        allVisibleIds.forEach((id) => next.delete(id));
        return next;
      }
      return new Set([...prev, ...allVisibleIds]);
    });
  };

  const clearSelection = () => setSelectedIds(new Set());

  const confirmBulk = (comment: string | undefined) => {
    if (!pendingBulkAction || selectedIds.size === 0) return;
    batchExecute(
      { taskIds: Array.from(selectedIds), action: pendingBulkAction, comment },
      {
        onSuccess: (result) => {
          setBulkResult(result);
          clearSelection();
        },
        onSettled: () => setPendingBulkAction(null),
      },
    );
  };

  return (
    <div className="space-y-6">
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
                <TableHead className="w-10">
                  <Checkbox
                    aria-label={t('workflow.inbox.selectAll')}
                    checked={allSelected}
                    onCheckedChange={toggleAll}
                  />
                </TableHead>
                <TableHead>{t('workflow.inbox.request')}</TableHead>
                <TableHead>{t('workflow.inbox.workflowName')}</TableHead>
                <TableHead>{t('workflow.inbox.step')}</TableHead>
                <TableHead>{t('workflow.inbox.assignedDate')}</TableHead>
                <TableHead>{t('workflow.inbox.dueDate')}</TableHead>
                <TableHead>{t('workflow.inbox.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tasks.map((task) => (
                <TableRow key={task.taskId}>
                  <TableCell>
                    <Checkbox
                      aria-label={t('workflow.inbox.select')}
                      checked={selectedIds.has(task.taskId)}
                      onCheckedChange={() => toggleOne(task.taskId)}
                    />
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 flex-wrap">
                      <Badge variant="secondary">{task.entityType}</Badge>
                      <span className="text-sm text-foreground truncate max-w-[200px]">
                        {task.entityDisplayName ?? task.entityId.substring(0, 8) + '...'}
                      </span>
                      {task.isOverdue && (
                        <Badge variant="destructive">
                          {t('workflow.sla.overdueHours', { hours: task.hoursOverdue ?? 0 })}
                        </Badge>
                      )}
                      {task.isDelegated && (
                        <Badge variant="secondary">
                          {t('workflow.delegation.badgeFrom', { name: task.delegatedFromDisplayName })}
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="text-foreground">{task.definitionName}</TableCell>
                  <TableCell className="text-foreground">{task.stepName}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatDate(task.createdAt)}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {task.dueDate ? formatDate(task.dueDate) : '—'}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Button size="sm" onClick={() => setSelectedTask(task)}>
                        {t('workflow.inbox.approve')}
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => setSelectedTask(task)}
                      >
                        {t('workflow.inbox.reject')}
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {pagination && (
            <Pagination
              pagination={pagination}
              onPageChange={setPage}
              onPageSizeChange={setPageSize}
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
        onClose={() => setBulkResult(null)}
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
