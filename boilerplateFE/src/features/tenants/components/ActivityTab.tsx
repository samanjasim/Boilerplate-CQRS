import { useState, Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronDown, ChevronRight, ClipboardList } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Spinner } from '@/components/ui/spinner';
import { Pagination, getPersistedPageSize, EmptyState } from '@/components/common';
import { useAuditLogs } from '@/features/audit-logs/api';
import { formatDateTime } from '@/utils/format';
import { AUDIT_ACTION_VARIANTS } from '@/constants';

function ChangesDetail({ changes }: { changes: string | null }) {
  const { t } = useTranslation();
  if (!changes) return null;

  let parsed: Record<string, unknown> | null = null;
  try {
    parsed = JSON.parse(changes);
  } catch {
    // invalid JSON
  }

  if (!parsed) {
    return <pre className="overflow-auto rounded bg-muted p-4 text-xs">{changes}</pre>;
  }

  return (
    <div className="grid gap-4 p-4 sm:grid-cols-2">
      {'OldValues' in parsed && parsed.OldValues != null && (
        <div>
          <p className="mb-2 text-xs font-semibold text-muted-foreground">{t('auditLogs.oldValues')}</p>
          <pre className="overflow-auto rounded bg-muted p-2 text-xs">{JSON.stringify(parsed.OldValues, null, 2)}</pre>
        </div>
      )}
      {'NewValues' in parsed && parsed.NewValues != null && (
        <div>
          <p className="mb-2 text-xs font-semibold text-muted-foreground">{t('auditLogs.newValues')}</p>
          <pre className="overflow-auto rounded bg-muted p-2 text-xs">{JSON.stringify(parsed.NewValues, null, 2)}</pre>
        </div>
      )}
      {!('OldValues' in parsed) && !('NewValues' in parsed) && (
        <pre className="overflow-auto rounded bg-muted p-2 text-xs sm:col-span-2">{JSON.stringify(parsed, null, 2)}</pre>
      )}
    </div>
  );
}

export function ActivityTab() {
  const { t } = useTranslation();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize());
  const [expandedRow, setExpandedRow] = useState<string | null>(null);

  const { data, isLoading } = useAuditLogs({
    pageNumber,
    pageSize,
    sortBy: 'performedAt',
    sortDescending: true,
  });

  const logs = data?.data ?? [];
  const pagination = data?.pagination;

  const toggleRow = (id: string) => {
    setExpandedRow((prev) => (prev === id ? null : id));
  };

  if (isLoading) {
    return <div className="flex justify-center py-12"><Spinner size="lg" /></div>;
  }

  if (logs.length === 0) {
    return <EmptyState icon={ClipboardList} title={t('auditLogs.noLogs')} />;
  }

  return (
    <div className="space-y-4">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-10" />
            <TableHead>{t('auditLogs.entityType')}</TableHead>
            <TableHead>{t('auditLogs.action')}</TableHead>
            <TableHead>{t('auditLogs.performedBy')}</TableHead>
            <TableHead>{t('auditLogs.date')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {logs.map((log) => (
            <Fragment key={log.id}>
              <TableRow
                className="cursor-pointer"
                onClick={() => toggleRow(log.id)}
              >
                <TableCell>
                  {expandedRow === log.id
                    ? <ChevronDown className="h-4 w-4 text-muted-foreground" />
                    : <ChevronRight className="h-4 w-4 text-muted-foreground" />}
                </TableCell>
                <TableCell className="text-foreground">
                  {log.entityType}
                  <span className="text-muted-foreground text-xs ml-1">{log.entityId?.substring(0, 8)}...</span>
                </TableCell>
                <TableCell>
                  <Badge variant={AUDIT_ACTION_VARIANTS[log.action] ?? 'secondary'}>
                    {log.action}
                  </Badge>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {log.performedByName || log.performedBy || '-'}
                </TableCell>
                <TableCell className="text-muted-foreground text-sm">
                  {formatDateTime(log.performedAt)}
                </TableCell>
              </TableRow>
              {expandedRow === log.id && (
                <TableRow>
                  <TableCell colSpan={5} className="bg-secondary/30 p-0">
                    <ChangesDetail changes={log.changes} />
                  </TableCell>
                </TableRow>
              )}
            </Fragment>
          ))}
        </TableBody>
      </Table>

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}
    </div>
  );
}
