import { useTranslation } from 'react-i18next';
import { Download } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '../utils/badges';
import { downloadImportErrors } from '../utils/download';
import type { ImportJob } from '@/types';

interface ImportProgressCardProps {
  job: ImportJob;
}

export function ImportProgressCard({ job }: ImportProgressCardProps) {
  const { t } = useTranslation();

  const isActive = ['Pending', 'Validating', 'Processing'].includes(job.status);
  const progressPct =
    job.totalRows > 0 ? Math.round((job.processedRows / job.totalRows) * 100) : 0;

  const handleDownloadErrors = () => {
    downloadImportErrors(job.id, t('common.somethingWentWrong'));
  };

  return (
    <div className="space-y-4 rounded-xl border bg-card p-4 shadow-sm">
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div className="space-y-0.5">
          <p className="text-sm font-medium text-foreground">{job.entityType}</p>
          <p className="text-xs text-muted-foreground truncate max-w-xs">{job.fileName}</p>
        </div>
        <StatusBadge job={job} />
      </div>

      {/* Progress bar */}
      {(isActive || job.totalRows > 0) && (
        <div className="space-y-1">
          <div className="flex justify-between text-xs text-muted-foreground">
            <span>
              {job.processedRows} / {job.totalRows} {t('importExport.totalRows').toLowerCase()}
            </span>
            <span>{progressPct}%</span>
          </div>
          <div className="h-2 w-full rounded-full bg-muted overflow-hidden">
            <div
              className="h-full rounded-full bg-primary transition-all duration-500"
              style={{ width: `${progressPct}%` }}
            />
          </div>
        </div>
      )}

      {/* Result counters */}
      {(job.createdCount > 0 ||
        job.updatedCount > 0 ||
        job.skippedCount > 0 ||
        job.failedCount > 0 ||
        !isActive) && (
        <div className="grid grid-cols-4 gap-2">
          <div className="flex flex-col items-center rounded-lg bg-green-50 dark:bg-green-950/20 p-2">
            <span className="text-lg font-semibold text-green-600">{job.createdCount}</span>
            <span className="text-xs text-green-600">{t('importExport.created')}</span>
          </div>
          <div className="flex flex-col items-center rounded-lg bg-blue-50 dark:bg-blue-950/20 p-2">
            <span className="text-lg font-semibold text-blue-600">{job.updatedCount}</span>
            <span className="text-xs text-blue-600">{t('importExport.updated')}</span>
          </div>
          <div className="flex flex-col items-center rounded-lg bg-amber-50 dark:bg-amber-950/20 p-2">
            <span className="text-lg font-semibold text-amber-600">{job.skippedCount}</span>
            <span className="text-xs text-amber-600">{t('importExport.skipped')}</span>
          </div>
          <div className="flex flex-col items-center rounded-lg bg-red-50 dark:bg-red-950/20 p-2">
            <span className="text-lg font-semibold text-red-600">{job.failedCount}</span>
            <span className="text-xs text-red-600">{t('importExport.failed')}</span>
          </div>
        </div>
      )}

      {/* Error message */}
      {job.errorMessage && (
        <p className="text-sm text-destructive">{job.errorMessage}</p>
      )}

      {/* Download error report */}
      {job.hasErrorReport && (
        <Button
          variant="outline"
          size="sm"
          className="w-full"
          onClick={handleDownloadErrors}
        >
          <Download className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
          {t('importExport.downloadErrors')}
        </Button>
      )}
    </div>
  );
}
