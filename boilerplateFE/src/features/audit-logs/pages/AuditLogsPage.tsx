import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { format } from 'date-fns';
import { ChevronDown, ChevronRight, ClipboardList } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState } from '@/components/common';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAuditLogs } from '../api';
import type { AuditLog } from '@/types';

const ACTION_VARIANTS: Record<string, 'default' | 'secondary' | 'destructive'> = {
  Created: 'default',
  Updated: 'secondary',
  Deleted: 'destructive',
};

function ChangesDetail({ changes }: { changes: string | null }) {
  const { t } = useTranslation();

  if (!changes) return null;

  try {
    const parsed = JSON.parse(changes);
    return (
      <div className="grid gap-4 p-4 sm:grid-cols-2">
        {parsed.OldValues && (
          <div>
            <p className="mb-2 text-xs font-semibold text-muted-foreground">
              {t('auditLogs.oldValues')}
            </p>
            <pre className="overflow-auto rounded bg-muted p-2 text-xs">
              {JSON.stringify(parsed.OldValues, null, 2)}
            </pre>
          </div>
        )}
        {parsed.NewValues && (
          <div>
            <p className="mb-2 text-xs font-semibold text-muted-foreground">
              {t('auditLogs.newValues')}
            </p>
            <pre className="overflow-auto rounded bg-muted p-2 text-xs">
              {JSON.stringify(parsed.NewValues, null, 2)}
            </pre>
          </div>
        )}
        {!parsed.OldValues && !parsed.NewValues && (
          <pre className="overflow-auto rounded bg-muted p-2 text-xs sm:col-span-2">
            {JSON.stringify(parsed, null, 2)}
          </pre>
        )}
      </div>
    );
  } catch {
    return (
      <pre className="overflow-auto rounded bg-muted p-4 text-xs">{changes}</pre>
    );
  }
}

export default function AuditLogsPage() {
  const { t } = useTranslation();
  const [expandedRow, setExpandedRow] = useState<string | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [entityType, setEntityType] = useState<string>('all');
  const [action, setAction] = useState<string>('all');
  const [searchTerm, setSearchTerm] = useState('');

  const params = useMemo(() => {
    const p: Record<string, unknown> = { pageNumber, pageSize: 20 };
    if (entityType && entityType !== 'all') p.entityType = entityType;
    if (action && action !== 'all') p.action = action;
    if (searchTerm) p.searchTerm = searchTerm;
    return p;
  }, [pageNumber, entityType, action, searchTerm]);

  const { data, isLoading, isError } = useAuditLogs(params);
  const logs = data?.data ?? [];
  const pagination = data?.pagination;

  const toggleRow = (id: string) => {
    setExpandedRow((prev) => (prev === id ? null : id));
  };

  const formatDate = (dateStr: string) => {
    return format(new Date(dateStr), 'MMM d, yyyy HH:mm');
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('auditLogs.title')} />
        <EmptyState icon={ClipboardList} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('auditLogs.title')} />

      {/* Filters */}
      <Card>
        <CardContent className="py-4">
          <div className="flex flex-wrap items-center gap-4">
            <div className="w-48">
              <Select value={entityType} onValueChange={(v) => { setEntityType(v); setPageNumber(1); }}>
                <SelectTrigger>
                  <SelectValue placeholder={t('auditLogs.filterByEntity')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('auditLogs.allEntities')}</SelectItem>
                  <SelectItem value="User">{t('auditLogs.user')}</SelectItem>
                  <SelectItem value="Role">{t('auditLogs.role')}</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="w-48">
              <Select value={action} onValueChange={(v) => { setAction(v); setPageNumber(1); }}>
                <SelectTrigger>
                  <SelectValue placeholder={t('auditLogs.filterByAction')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('auditLogs.allActions')}</SelectItem>
                  <SelectItem value="Created">{t('auditLogs.created')}</SelectItem>
                  <SelectItem value="Updated">{t('auditLogs.updated')}</SelectItem>
                  <SelectItem value="Deleted">{t('auditLogs.deleted')}</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="flex-1 min-w-[200px]">
              <Input
                placeholder={t('common.search')}
                value={searchTerm}
                onChange={(e) => { setSearchTerm(e.target.value); setPageNumber(1); }}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Table */}
      {logs.length === 0 ? (
        <EmptyState icon={ClipboardList} title={t('auditLogs.noLogs')} />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-10" />
                  <TableHead>{t('auditLogs.entityType')}</TableHead>
                  <TableHead>{t('auditLogs.action')}</TableHead>
                  <TableHead>{t('auditLogs.performedBy')}</TableHead>
                  <TableHead>{t('auditLogs.performedAt')}</TableHead>
                  <TableHead>{t('auditLogs.ipAddress')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {logs.map((log: AuditLog) => (
                  <>
                    <TableRow
                      key={log.id}
                      className="cursor-pointer"
                      onClick={() => log.changes && toggleRow(log.id)}
                    >
                      <TableCell>
                        {log.changes && (
                          expandedRow === log.id ? (
                            <ChevronDown className="h-4 w-4 text-muted-foreground" />
                          ) : (
                            <ChevronRight className="h-4 w-4 text-muted-foreground" />
                          )
                        )}
                      </TableCell>
                      <TableCell>
                        <span className="font-medium">{log.entityType}</span>
                        <span className="ml-1 text-xs text-muted-foreground">
                          {log.entityId.substring(0, 8)}...
                        </span>
                      </TableCell>
                      <TableCell>
                        <Badge variant={ACTION_VARIANTS[log.action] ?? 'secondary'}>
                          {log.action}
                        </Badge>
                      </TableCell>
                      <TableCell>{log.performedByName ?? '-'}</TableCell>
                      <TableCell>{formatDate(log.performedAt)}</TableCell>
                      <TableCell>{log.ipAddress ?? '-'}</TableCell>
                    </TableRow>
                    {expandedRow === log.id && log.changes && (
                      <TableRow key={`${log.id}-changes`}>
                        <TableCell colSpan={6} className="bg-muted/50 p-0">
                          <ChangesDetail changes={log.changes} />
                        </TableCell>
                      </TableRow>
                    )}
                  </>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Pagination */}
      {pagination && pagination.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            {t('common.showing', {
              start: (pagination.pageNumber - 1) * pagination.pageSize + 1,
              end: Math.min(pagination.pageNumber * pagination.pageSize, pagination.totalCount),
              total: pagination.totalCount,
            })}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={!pagination.hasPreviousPage}
              onClick={() => setPageNumber((p) => p - 1)}
            >
              {t('common.previous')}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={!pagination.hasNextPage}
              onClick={() => setPageNumber((p) => p + 1)}
            >
              {t('common.next')}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
