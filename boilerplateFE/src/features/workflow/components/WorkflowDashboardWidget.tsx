import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ClipboardCheck } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { usePendingTasks, usePendingTaskCount } from '../api';
import { formatDate } from '@/utils/format';
import type { PendingTaskSummary } from '@/types/workflow.types';

export function WorkflowDashboardWidget() {
  const { t } = useTranslation();
  const { data: countData } = usePendingTaskCount();
  const { data: tasksData } = usePendingTasks({ page: 1, pageSize: 5 });

  const count = typeof countData === 'number' ? countData : (countData as { count?: number })?.count ?? 0;
  const tasks: PendingTaskSummary[] = tasksData?.data ?? [];

  return (
    <Card className="hover-lift">
      <CardContent className="py-6">
        <div className="flex items-center gap-4">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-orange-500/10 text-orange-600">
            <ClipboardCheck className="h-6 w-6" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm text-muted-foreground">{t('workflow.sidebar.taskInbox')}</p>
            <div className="flex items-center gap-2">
              <p className="text-2xl font-bold text-foreground">{count}</p>
              {count > 0 && (
                <Badge variant="secondary" className="text-xs">{t('workflow.status.pending')}</Badge>
              )}
            </div>
          </div>
        </div>

        {tasks.length > 0 && (
          <div className="mt-4 space-y-2">
            {tasks.map((task) => (
              <div
                key={task.taskId}
                className="flex items-center justify-between rounded-lg px-3 py-2 text-sm hover:bg-secondary transition-colors"
              >
                <div className="flex items-center gap-2 min-w-0">
                  <Badge variant="secondary" className="text-xs shrink-0">{task.entityType}</Badge>
                  <span className="text-foreground truncate">{task.definitionName}</span>
                </div>
                <span className="text-xs text-muted-foreground shrink-0">
                  {formatDate(task.createdAt)}
                </span>
              </div>
            ))}
          </div>
        )}

        {count > 0 && (
          <div className="mt-4">
            <Button variant="link" size="sm" asChild className="px-0">
              <Link to="/workflows/inbox">{t('workflow.inbox.viewAll')}</Link>
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
