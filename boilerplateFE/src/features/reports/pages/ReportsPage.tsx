import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { FileText, Download, Trash2, RefreshCw, AlertCircle } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader, EmptyState, ConfirmDialog, Pagination, getPersistedPageSize } from '@/components/common';
import { useReports, useDownloadReport, useDeleteReport, useRequestReport } from '../api';
import { ReportStatusHeroStrip } from '../components/ReportStatusHeroStrip';
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
      return (
        <Badge variant="destructive">
          {t('reports.failed')}
        </Badge>
      );
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}

export default function ReportsPage() {
  const { t } = useTranslation();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [reportType, setReportType] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [deleteTarget, setDeleteTarget] = useState<ReportRequest | null>(null);

  const params = useMemo(() => {
    const p: Record<string, unknown> = { pageNumber, pageSize };
    if (reportType && reportType !== 'all') p.reportType = reportType;
    if (statusFilter && statusFilter !== 'all') p.status = statusFilter;
    return p;
  }, [pageNumber, pageSize, reportType, statusFilter]);

  const { data, isLoading, isFetching, isError } = useReports(params);
  const { mutate: downloadReport, isPending: isDownloading } = useDownloadReport();
  const { mutate: deleteReport, isPending: isDeleting } = useDeleteReport();
  const { mutate: requestReport } = useRequestReport();

  const reports: ReportRequest[] = data?.data ?? [];
  const pagination = data?.pagination;

  const formatDate = (dateStr: string) => {
    return formatDateTime(dateStr);
  };

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
    const data: RequestReportData = {
      reportType: report.reportType,
      format: report.format,
      forceRefresh: true,
    };
    if (report.filters) {
      data.filters = report.filters;
    }
    requestReport(data);
  };

  const handleDeleteConfirm = () => {
    if (!deleteTarget) return;
    deleteReport(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
      onError: () => { setDeleteTarget(null); },
    });
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('reports.title')} />
        <EmptyState
          icon={FileText}
          title={t('common.errorOccurred')}
          description={t('common.tryAgain')}
        />
      </div>
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
    <div className="space-y-6">
      <PageHeader
        title={t('reports.title')}
        subtitle={t('reports.subtitle')}
      />

      <ReportStatusHeroStrip />

      {/* Filters */}
      <Card>
        <CardContent className="py-4">
          <div className="flex flex-wrap items-center gap-4">
            <div className="w-48">
              <Select
                value={reportType}
                onValueChange={(v) => { setReportType(v); setPageNumber(1); }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t('reports.reportType')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('reports.reportType')}</SelectItem>
                  <SelectItem value="AuditLogs">{t('reports.auditLogs')}</SelectItem>
                  <SelectItem value="Users">{t('reports.users')}</SelectItem>
                  <SelectItem value="Files">{t('reports.files')}</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="w-48">
              <Select
                value={statusFilter}
                onValueChange={(v) => { setStatusFilter(v); setPageNumber(1); }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t('reports.status')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('reports.status')}</SelectItem>
                  <SelectItem value="Pending">{t('reports.pending')}</SelectItem>
                  <SelectItem value="Processing">{t('reports.processing')}</SelectItem>
                  <SelectItem value="Completed">{t('reports.completed')}</SelectItem>
                  <SelectItem value="Failed">{t('reports.failed')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

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
          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('reports.reportType')}</TableHead>
                    <TableHead>{t('reports.format')}</TableHead>
                    <TableHead>{t('reports.status')}</TableHead>
                    <TableHead>{t('reports.requestedAt')}</TableHead>
                    <TableHead>{t('reports.actions')}</TableHead>
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
                        {formatDate(report.requestedAt)}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
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
            </CardContent>
          </Card>
        )}
      </div>

      {/* Pagination */}
      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      {/* Delete Confirmation */}
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
