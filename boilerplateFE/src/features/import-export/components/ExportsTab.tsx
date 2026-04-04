import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { FileText, Download, Trash2, RefreshCw, AlertCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { EmptyState, ConfirmDialog, ExportButton, Pagination, getPersistedPageSize } from '@/components/common';
import {
  useReports,
  useDownloadReport,
  useDeleteReport,
  useRequestReport,
} from '@/features/reports/api';
import type { ReportRequest, RequestReportData } from '@/types';

function StatusBadge({ status }: { status: string }) {
  const { t } = useTranslation();

  switch (status) {
    case 'Pending':
      return (
        <Badge variant="secondary" className="gap-1">
          <Spinner size="sm" className="h-3 w-3" />
          {t('reports.pending')}
        </Badge>
      );
    case 'Processing':
      return (
        <Badge variant="default" className="gap-1">
          <Spinner size="sm" className="h-3 w-3" />
          {t('reports.processing')}
        </Badge>
      );
    case 'Completed':
      return (
        <Badge variant="outline" className="border-green-500 text-green-600">
          {t('reports.completed')}
        </Badge>
      );
    case 'Failed':
      return <Badge variant="destructive">{t('reports.failed')}</Badge>;
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}

export function ExportsTab() {
  const { t } = useTranslation();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [deleteTarget, setDeleteTarget] = useState<ReportRequest | null>(null);

  const params = useMemo(
    () => ({ pageNumber, pageSize }),
    [pageNumber, pageSize]
  );

  const { data, isLoading, isFetching, isError } = useReports(params);
  const { mutate: downloadReport, isPending: isDownloading } = useDownloadReport();
  const { mutate: deleteReport, isPending: isDeleting } = useDeleteReport();
  const { mutate: requestReport } = useRequestReport();

  const reports: ReportRequest[] = data?.data ?? [];
  const pagination = data?.pagination;

  const reportTypeLabel = (type: string) => {
    switch (type) {
      case 'AuditLogs': return t('reports.auditLogs');
      case 'Users': return t('reports.users');
      case 'Files': return t('reports.files');
      default: return type;
    }
  };

  const formatLabel = (fmt: string) => {
    switch (fmt) {
      case 'Csv': return t('reports.csv');
      case 'Pdf': return t('reports.pdf');
      default: return fmt;
    }
  };

  const handleRetry = (report: ReportRequest) => {
    const retryData: RequestReportData = {
      reportType: report.reportType,
      format: report.format,
      forceRefresh: true,
    };
    if (report.filters) retryData.filters = report.filters;
    requestReport(retryData);
  };

  const handleDeleteConfirm = () => {
    if (!deleteTarget) return;
    deleteReport(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
      onError: () => setDeleteTarget(null),
    });
  };

  if (isError) {
    return (
      <EmptyState
        icon={FileText}
        title={t('common.errorOccurred')}
        description={t('common.tryAgain')}
      />
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Export button */}
      <div className="flex justify-end">
        <ExportButton reportType="Users" canForceRefresh />
      </div>

      {/* Table */}
      <div className={`relative transition-opacity ${isFetching && !isLoading ? 'opacity-60' : ''}`}>
        {isFetching && !isLoading && (
          <div className="absolute inset-0 z-10 flex items-start justify-center pt-12">
            <Spinner size="md" />
          </div>
        )}
        {reports.length === 0 && !isFetching ? (
          <EmptyState
            icon={FileText}
            title={t('reports.noReports')}
            description={t('reports.noReportsDesc')}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('reports.reportType')}</TableHead>
                <TableHead>{t('reports.format')}</TableHead>
                <TableHead>{t('reports.status')}</TableHead>
                <TableHead>{t('reports.requestedAt')}</TableHead>
                <TableHead className="text-end">{t('common.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {reports.map((report) => (
                <TableRow key={report.id}>
                  <TableCell className="font-medium">
                    {reportTypeLabel(report.reportType)}
                  </TableCell>
                  <TableCell>
                    <Badge variant="secondary">{formatLabel(report.format)}</Badge>
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={report.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDateTime(report.requestedAt)}
                  </TableCell>
                  <TableCell className="text-end">
                    <div className="flex items-center justify-end gap-1">
                      {report.status === 'Completed' && (
                        <>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => downloadReport(report.id)}
                            disabled={isDownloading}
                            title={t('reports.download')}
                          >
                            <Download className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setDeleteTarget(report)}
                            title={t('reports.delete')}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </>
                      )}
                      {report.status === 'Failed' && (
                        <>
                          <span
                            className="inline-flex items-center"
                            title={report.errorMessage ?? t('reports.reportFailed')}
                          >
                            <AlertCircle className="h-4 w-4 text-destructive" />
                          </span>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleRetry(report)}
                            title={t('reports.retry')}
                          >
                            <RefreshCw className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setDeleteTarget(report)}
                            title={t('reports.delete')}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </>
                      )}
                      {(report.status === 'Pending' || report.status === 'Processing') && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setDeleteTarget(report)}
                          disabled={report.status === 'Processing'}
                          title={t('reports.cancel')}
                        >
                          <Trash2 className="h-4 w-4 text-muted-foreground" />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      {/* Pagination */}
      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPageNumber(1);
          }}
        />
      )}

      {/* Delete confirmation */}
      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDeleteConfirm}
        title={t('reports.delete')}
        description={t('reports.deleteConfirm')}
        isLoading={isDeleting}
      />
    </div>
  );
}
