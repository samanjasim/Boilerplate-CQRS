import { useMemo, useState, Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { ChevronDown, ChevronRight, ClipboardList } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import {
  PageHeader,
  ExportButton,
  Pagination,
  ListPageState,
  ListToolbar,
  DateRangePicker,
  type DateRange,
} from '@/components/common';
import { usePermissions, useListPage } from '@/hooks';
import { PERMISSIONS, AUDIT_ACTION_VARIANTS } from '@/constants';
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

interface AuditLogFilters {
  searchTerm?: string;
  entityType?: string;
  action?: string;
  dateFrom?: string;
  dateTo?: string;
}

function ChangesDetail({ changes }: { changes: string | null }) {
  const { t } = useTranslation();

  if (!changes) return null;

  let parsed: Record<string, unknown> | null = null;
  try {
    parsed = JSON.parse(changes);
  } catch {
    // invalid JSON — falls through to raw render below
  }

  if (!parsed) {
    return <pre className="overflow-auto rounded bg-muted p-4 text-xs">{changes}</pre>;
  }

  return (
    <div className="grid gap-4 p-4 sm:grid-cols-2">
      {'OldValues' in parsed && parsed.OldValues != null && (
        <div>
          <p className="mb-2 text-xs font-semibold text-muted-foreground">
            {t('auditLogs.oldValues')}
          </p>
          <pre className="overflow-auto rounded bg-muted p-2 text-xs">
            {JSON.stringify(parsed.OldValues, null, 2)}
          </pre>
        </div>
      )}
      {'NewValues' in parsed && parsed.NewValues != null && (
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
}

export default function AuditLogsPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.System.ExportData);
  const [expandedRow, setExpandedRow] = useState<string | null>(null);

  const list = useListPage<AuditLogFilters, AuditLog>({ queryHook: useAuditLogs });

  const exportFilters = useMemo(() => {
    const f: Record<string, unknown> = {};
    if (list.filters.entityType) f.entityType = list.filters.entityType;
    if (list.filters.action) f.action = list.filters.action;
    if (list.filters.searchTerm) f.searchTerm = list.filters.searchTerm;
    if (list.filters.dateFrom) f.dateFrom = list.filters.dateFrom;
    if (list.filters.dateTo) f.dateTo = list.filters.dateTo;
    return f;
  }, [list.filters]);

  const dateRange: DateRange = {
    from: list.filters.dateFrom,
    to: list.filters.dateTo,
  };

  const handleDateRangeChange = (next: DateRange) => {
    list.setFilter('dateFrom', next.from ?? '');
    list.setFilter('dateTo', next.to ?? '');
  };

  const toggleRow = (id: string) => {
    setExpandedRow((prev) => (prev === id ? null : id));
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('auditLogs.title')}
        actions={canExport ? <ExportButton reportType="AuditLogs" filters={exportFilters} /> : undefined}
      />

      <ListToolbar
        search={{
          value: list.filters.searchTerm ?? '',
          onChange: (v) => list.setFilter('searchTerm', v),
        }}
        filters={
          <>
            <Select
              value={list.filters.entityType ?? 'all'}
              onValueChange={(v) => list.setFilter('entityType', v === 'all' ? '' : v)}
            >
              <SelectTrigger className="w-48">
                <SelectValue placeholder={t('auditLogs.filterByEntity')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t('auditLogs.allEntities')}</SelectItem>
                <SelectItem value="User">{t('auditLogs.user')}</SelectItem>
                <SelectItem value="Role">{t('auditLogs.role')}</SelectItem>
              </SelectContent>
            </Select>

            <Select
              value={list.filters.action ?? 'all'}
              onValueChange={(v) => list.setFilter('action', v === 'all' ? '' : v)}
            >
              <SelectTrigger className="w-48">
                <SelectValue placeholder={t('auditLogs.filterByAction')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t('auditLogs.allActions')}</SelectItem>
                <SelectItem value="Created">{t('auditLogs.created')}</SelectItem>
                <SelectItem value="Updated">{t('auditLogs.updated')}</SelectItem>
                <SelectItem value="Deleted">{t('auditLogs.deleted')}</SelectItem>
              </SelectContent>
            </Select>

            <DateRangePicker
              value={dateRange}
              onChange={handleDateRangeChange}
              placeholder={t('auditLogs.dateRange')}
            />
          </>
        }
      />

      <div
        className={`relative transition-opacity ${
          list.isFetching && !list.isInitialLoading ? 'opacity-60' : ''
        }`}
      >
        {list.isFetching && !list.isInitialLoading && (
          <div className="absolute inset-0 z-10 flex items-start justify-center pt-12">
            <Spinner size="md" />
          </div>
        )}

        <ListPageState
          isInitialLoading={list.isInitialLoading}
          isError={list.isError}
          isEmpty={list.isEmpty}
          emptyState={{ icon: ClipboardList, title: t('auditLogs.noLogs') }}
        >
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
              {list.data.map((log) => (
                <Fragment key={log.id}>
                  <TableRow
                    className="cursor-pointer"
                    onClick={() => log.changes && toggleRow(log.id)}
                  >
                    <TableCell>
                      {log.changes &&
                        (expandedRow === log.id ? (
                          <ChevronDown className="h-4 w-4 text-muted-foreground" />
                        ) : (
                          <ChevronRight className="h-4 w-4 text-muted-foreground rtl:rotate-180" />
                        ))}
                    </TableCell>
                    <TableCell>
                      <span className="font-medium">{log.entityType}</span>
                      <span className="ms-1 text-xs text-muted-foreground">
                        {log.entityId.substring(0, 8)}...
                      </span>
                    </TableCell>
                    <TableCell>
                      <Badge variant={AUDIT_ACTION_VARIANTS[log.action] ?? 'secondary'}>
                        {log.action}
                      </Badge>
                    </TableCell>
                    <TableCell>{log.performedByName ?? '-'}</TableCell>
                    <TableCell>{formatDateTime(log.performedAt)}</TableCell>
                    <TableCell>{log.ipAddress ?? '-'}</TableCell>
                  </TableRow>
                  {expandedRow === log.id && log.changes && (
                    <TableRow key={`${log.id}-changes`}>
                      <TableCell colSpan={6} className="bg-muted/50 p-0">
                        <ChangesDetail changes={log.changes} />
                      </TableCell>
                    </TableRow>
                  )}
                </Fragment>
              ))}
            </TableBody>
          </Table>
        </ListPageState>
      </div>

      {list.pagination && (
        <Pagination
          pagination={list.pagination}
          onPageChange={list.setPage}
          onPageSizeChange={list.setPageSize}
        />
      )}
    </div>
  );
}
