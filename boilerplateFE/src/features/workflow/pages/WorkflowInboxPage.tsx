import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inbox } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { usePendingTasks } from '../api';
import { ApprovalDialog } from '../components/ApprovalDialog';
import { formatDate } from '@/utils/format';
import type { PendingTaskSummary } from '@/types/workflow.types';

export default function WorkflowInboxPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [selectedTask, setSelectedTask] = useState<PendingTaskSummary | null>(null);

  const { data, isLoading } = usePendingTasks({ page, pageSize });
  const tasks: PendingTaskSummary[] = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <PageHeader title={t('workflow.inbox.title')} />

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
                <TableHead>{t('workflow.inbox.entity')}</TableHead>
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
                    <div className="flex items-center gap-2">
                      <Badge variant="secondary">{task.entityType}</Badge>
                      <span className="text-sm text-muted-foreground truncate max-w-[120px]">
                        {task.entityId.substring(0, 8)}...
                      </span>
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

      {selectedTask && (
        <ApprovalDialog
          taskId={selectedTask.taskId}
          definitionName={selectedTask.definitionName}
          entityType={selectedTask.entityType}
          entityId={selectedTask.entityId}
          actions={['Approve', 'Reject', 'ReturnForRevision']}
          open={!!selectedTask}
          onOpenChange={(open) => { if (!open) setSelectedTask(null); }}
        />
      )}
    </div>
  );
}
