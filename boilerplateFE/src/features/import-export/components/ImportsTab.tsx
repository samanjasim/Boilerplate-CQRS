import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload, Trash2, Download } from 'lucide-react';
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
import { EmptyState, ConfirmDialog, Pagination, getPersistedPageSize } from '@/components/common';
import { ImportWizard } from './ImportWizard';
import { useImportJobs, useDeleteImportJob } from '../api';
import { StatusBadge } from '../utils/badges';
import { downloadImportErrors } from '../utils/download';
import { formatDistanceToNow } from 'date-fns';
import type { ImportJob } from '@/types';

function ProgressCell({ job }: { job: ImportJob }) {
  const isActive = ['Pending', 'Validating', 'Processing'].includes(job.status);
  if (!isActive && job.totalRows === 0) return <span className="text-muted-foreground text-sm">—</span>;

  const pct = job.totalRows > 0 ? Math.round((job.processedRows / job.totalRows) * 100) : 0;

  if (isActive) {
    return (
      <div className="space-y-1 min-w-[100px]">
        <div className="text-xs text-muted-foreground">
          {job.processedRows} / {job.totalRows}
        </div>
        <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
          <div
            className="h-full rounded-full bg-primary transition-all duration-500"
            style={{ width: `${pct}%` }}
          />
        </div>
      </div>
    );
  }

  return (
    <span className="text-sm text-muted-foreground">
      {job.processedRows} / {job.totalRows}
    </span>
  );
}

function ResultsCell({ job }: { job: ImportJob }) {
  const { t } = useTranslation();
  const isDone = ['Completed', 'PartialSuccess', 'Failed'].includes(job.status);
  if (!isDone) return <span className="text-muted-foreground text-sm">—</span>;

  return (
    <div className="flex flex-wrap gap-1">
      {job.createdCount > 0 && (
        <Badge variant="outline" className="text-xs border-green-500 text-green-600">
          +{job.createdCount} {t('importExport.created')}
        </Badge>
      )}
      {job.updatedCount > 0 && (
        <Badge variant="outline" className="text-xs border-blue-500 text-blue-600">
          ~{job.updatedCount} {t('importExport.updated')}
        </Badge>
      )}
      {job.skippedCount > 0 && (
        <Badge variant="outline" className="text-xs border-amber-500 text-amber-600">
          {job.skippedCount} {t('importExport.skipped')}
        </Badge>
      )}
      {job.failedCount > 0 && (
        <Badge variant="outline" className="text-xs border-red-500 text-red-600">
          {job.failedCount} {t('importExport.failed')}
        </Badge>
      )}
    </div>
  );
}

export function ImportsTab() {
  const { t } = useTranslation();
  const [wizardOpen, setWizardOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ImportJob | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);

  const { data, isLoading, isError } = useImportJobs({ pageNumber, pageSize });
  const deleteMutation = useDeleteImportJob();

  const jobs: ImportJob[] = data?.data ?? [];
  const pagination = data?.pagination;

  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return;
    await deleteMutation.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  };

  const handleDownloadErrors = (job: ImportJob) => {
    downloadImportErrors(job.id, t('common.somethingWentWrong'));
  };

  if (isError) {
    return (
      <EmptyState
        icon={Upload}
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
      {/* Start import button */}
      <div className="flex justify-end">
        <Button onClick={() => setWizardOpen(true)}>
          <Upload className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
          {t('importExport.startImport')}
        </Button>
      </div>

      {/* Table */}
      {jobs.length === 0 ? (
        <EmptyState
          icon={Upload}
          title={t('importExport.noImports')}
          description={t('importExport.subtitle')}
          action={{ label: t('importExport.startImport'), onClick: () => setWizardOpen(true) }}
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('importExport.selectEntityType')}</TableHead>
              <TableHead>{t('common.fileName', 'File Name')}</TableHead>
              <TableHead>{t('reports.status')}</TableHead>
              <TableHead>{t('importExport.processedRows')}</TableHead>
              <TableHead>{t('common.results', 'Results')}</TableHead>
              <TableHead>{t('common.createdAt', 'Created')}</TableHead>
              <TableHead className="text-end">{t('common.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {jobs.map((job) => (
              <TableRow key={job.id}>
                {/* Entity type */}
                <TableCell>
                  <Badge variant="secondary" className="text-xs">
                    {job.entityType}
                  </Badge>
                </TableCell>

                {/* File name */}
                <TableCell className="max-w-[180px] truncate text-sm text-muted-foreground" title={job.fileName}>
                  {job.fileName}
                </TableCell>

                {/* Status */}
                <TableCell>
                  <StatusBadge job={job} />
                </TableCell>

                {/* Progress */}
                <TableCell>
                  <ProgressCell job={job} />
                </TableCell>

                {/* Results */}
                <TableCell>
                  <ResultsCell job={job} />
                </TableCell>

                {/* Created at */}
                <TableCell className="text-xs text-muted-foreground whitespace-nowrap">
                  {formatDistanceToNow(new Date(job.createdAt), { addSuffix: true })}
                </TableCell>

                {/* Actions */}
                <TableCell className="text-end">
                  <div className="flex justify-end gap-1">
                    {job.hasErrorReport && (
                      <Button
                        variant="ghost"
                        size="icon"
                        title={t('importExport.downloadErrors')}
                        onClick={() => handleDownloadErrors(job)}
                      >
                        <Download className="h-4 w-4" />
                      </Button>
                    )}
                    <Button
                      variant="ghost"
                      size="icon"
                      title={t('common.delete')}
                      onClick={() => setDeleteTarget(job)}
                      disabled={['Validating', 'Processing'].includes(job.status)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {/* Pagination */}
      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      {/* Wizard */}
      <ImportWizard open={wizardOpen} onOpenChange={setWizardOpen} />

      {/* Delete confirmation */}
      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title={t('common.delete')}
        description={t('importExport.deleteConfirm')}
        confirmLabel={t('common.delete')}
        onConfirm={handleDeleteConfirm}
        isLoading={deleteMutation.isPending}
        variant="danger"
      />
    </div>
  );
}
