import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { History, Eye } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { useWorkflowInstances } from '../api';
import { usePermissions } from '@/hooks';
import { useAuthStore, selectUser } from '@/stores';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';
import { formatDate } from '@/utils/format';
import type { WorkflowInstanceSummary } from '@/types/workflow.types';

const STATUS_FILTERS = ['All', 'Active', 'Completed', 'Cancelled'] as const;

const statusBadgeVariant: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Completed: 'secondary',
  Cancelled: 'destructive',
};

export default function WorkflowInstancesPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);
  const canViewAll = hasPermission(PERMISSIONS.Workflows.ViewAllTasks);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [statusFilter, setStatusFilter] = useState<string>('All');
  const [entityTypeFilter, setEntityTypeFilter] = useState('');
  const [myRequestsOnly, setMyRequestsOnly] = useState(!canViewAll);

  const startedByUserId = (!canViewAll || myRequestsOnly) ? user?.id : undefined;

  const { data, isLoading } = useWorkflowInstances({
    entityType: entityTypeFilter || undefined,
    status: statusFilter !== 'All' ? statusFilter : undefined,
    startedByUserId,
    page,
    pageSize,
  });

  const instances: WorkflowInstanceSummary[] = data?.data ?? [];
  const pagination = data?.pagination;

  const title = canViewAll
    ? t('workflow.instances.title')
    : t('workflow.instances.myRequests');

  return (
    <div className="space-y-6">
      <PageHeader title={title} />

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-4">
        <div className="space-y-1.5">
          <Label className="text-xs text-muted-foreground">
            {t('workflow.instances.status')}
          </Label>
          <Select value={statusFilter} onValueChange={(v) => { setStatusFilter(v); setPage(1); }}>
            <SelectTrigger className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {STATUS_FILTERS.map((s) => (
                <SelectItem key={s} value={s}>
                  {s === 'All' ? t('common.all', 'All') : t(`workflow.status.${s.toLowerCase()}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs text-muted-foreground">
            {t('workflow.instances.entityType')}
          </Label>
          <Input
            value={entityTypeFilter}
            onChange={(e) => { setEntityTypeFilter(e.target.value); setPage(1); }}
            placeholder={t('workflow.instances.entityType')}
            className="w-[180px]"
          />
        </div>

        {canViewAll && (
          <Button
            variant={myRequestsOnly ? 'default' : 'outline'}
            size="sm"
            className="self-end"
            onClick={() => setMyRequestsOnly((v) => !v)}
          >
            {t('workflow.instances.myRequestsOnly')}
          </Button>
        )}
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner size="lg" />
        </div>
      ) : instances.length === 0 ? (
        <EmptyState
          icon={History}
          title={t('workflow.instances.empty')}
          description={t('workflow.instances.emptyDesc')}
        />
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('workflow.instances.definitionName')}</TableHead>
                <TableHead>{t('workflow.instances.entityType')}</TableHead>
                <TableHead>{t('workflow.instances.entityId')}</TableHead>
                <TableHead>{t('workflow.instances.currentState')}</TableHead>
                <TableHead>{t('workflow.instances.status')}</TableHead>
                <TableHead>{t('workflow.instances.startedAt')}</TableHead>
                <TableHead>{t('workflow.instances.viewDetail')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {instances.map((instance) => (
                <TableRow key={instance.instanceId}>
                  <TableCell className="text-foreground font-medium">
                    {instance.definitionName}
                  </TableCell>
                  <TableCell>
                    <Badge variant="secondary">{instance.entityType}</Badge>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {instance.entityId.substring(0, 8)}...
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">{instance.currentState}</Badge>
                  </TableCell>
                  <TableCell>
                    <Badge variant={statusBadgeVariant[instance.status] ?? 'outline'}>
                      {t(`workflow.status.${instance.status.toLowerCase()}`)}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatDate(instance.startedAt)}
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="sm"
                      asChild
                    >
                      <Link
                        to={ROUTES.WORKFLOWS.getInstanceDetail(instance.instanceId)}
                        state={{ instance }}
                      >
                        <Eye className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                        {t('workflow.instances.viewDetail')}
                      </Link>
                    </Button>
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
    </div>
  );
}
