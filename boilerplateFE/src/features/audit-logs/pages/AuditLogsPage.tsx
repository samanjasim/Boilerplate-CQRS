import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { formatDateTime } from '@/utils/format';
import { ClipboardList } from 'lucide-react';
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
import { usePermissions, useListPage, useDebounce } from '@/hooks';
import { PERMISSIONS, AUDIT_ACTION_VARIANTS } from '@/constants';
import { ROUTES } from '@/config';
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
import { AuditTimelineHero } from '../components/AuditTimelineHero';
import type { AuditLog } from '@/types';

interface AuditLogFilters {
  searchTerm?: string;
  entityType?: string;
  action?: string;
  dateFrom?: string;
  dateTo?: string;
}

const DEFAULT_TIMELINE_WINDOW_MS = 24 * 60 * 60 * 1000;

// Mirrors backend Starter.Domain.Common.Enums.AuditEntityType. The select serialises by
// enum name (the BE handler maps the string back via Enum.Parse).
const ENTITY_TYPE_OPTIONS = [
  { value: 'User', labelKey: 'auditLogs.user' },
  { value: 'Role', labelKey: 'auditLogs.role' },
  { value: 'Tenant', labelKey: 'auditLogs.tenant' },
  { value: 'File', labelKey: 'auditLogs.file' },
  { value: 'ApiKey', labelKey: 'auditLogs.apiKey' },
  { value: 'ResourceGrant', labelKey: 'auditLogs.resourceGrant' },
  { value: 'AiAssistant', labelKey: 'auditLogs.aiAssistant' },
] as const;

// Mirrors backend Starter.Domain.Common.Enums.AuditAction.
const ACTION_OPTIONS = [
  { value: 'Created', labelKey: 'auditLogs.created' },
  { value: 'Updated', labelKey: 'auditLogs.updated' },
  { value: 'Deleted', labelKey: 'auditLogs.deleted' },
  { value: 'EmergencyRevoked', labelKey: 'auditLogs.emergencyRevoked' },
] as const;

export default function AuditLogsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.System.ExportData);

  const list = useListPage<AuditLogFilters, AuditLog>({ queryHook: useAuditLogs });

  const windowMs = useMemo(() => {
    if (list.filters.dateFrom && list.filters.dateTo) {
      return new Date(list.filters.dateTo).getTime() - new Date(list.filters.dateFrom).getTime();
    }
    return DEFAULT_TIMELINE_WINDOW_MS;
  }, [list.filters.dateFrom, list.filters.dateTo]);

  // Debounce filter changes before re-fetching the timeline; recompute `now` on each
  // accepted filter change so the rolling default-window stays aligned with wall clock
  // for an open page that's used over time.
  const debouncedFilters = useDebounce(list.filters, 500);
  const timelineFilters = useMemo(() => {
    const now = Date.now();
    return {
      ...debouncedFilters,
      pageNumber: 1,
      pageSize: 2000,
      dateFrom: debouncedFilters.dateFrom ?? new Date(now - windowMs).toISOString(),
      dateTo: debouncedFilters.dateTo ?? new Date(now).toISOString(),
      sortBy: 'performedAt',
      sortDescending: true,
    };
  }, [debouncedFilters, windowMs]);

  const heroNow = useMemo(
    () => new Date(timelineFilters.dateTo).getTime(),
    [timelineFilters.dateTo],
  );

  const { data: timelineData } = useAuditLogs(timelineFilters);
  const timelineRows = timelineData?.data ?? [];
  const timelineTotal = timelineData?.pagination?.totalCount ?? timelineRows.length;
  const truncated = timelineTotal > 2000;

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

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('auditLogs.title')}
        actions={canExport ? <ExportButton reportType="AuditLogs" filters={exportFilters} /> : undefined}
      />

      <AuditTimelineHero
        rows={timelineRows}
        totalCount={timelineTotal}
        windowMs={windowMs}
        now={heroNow}
        truncated={truncated}
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
                {ENTITY_TYPE_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>{t(opt.labelKey)}</SelectItem>
                ))}
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
                {ACTION_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>{t(opt.labelKey)}</SelectItem>
                ))}
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
                <TableHead>{t('auditLogs.entityType')}</TableHead>
                <TableHead>{t('auditLogs.action')}</TableHead>
                <TableHead>{t('auditLogs.performedBy')}</TableHead>
                <TableHead>{t('auditLogs.performedAt')}</TableHead>
                <TableHead>{t('auditLogs.ipAddress')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {list.data.map((log) => (
                <TableRow
                  key={log.id}
                  className="cursor-pointer hover:bg-[var(--hover-bg)]"
                  onClick={() => navigate(ROUTES.AUDIT_LOGS.getDetail(log.id))}
                >
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
