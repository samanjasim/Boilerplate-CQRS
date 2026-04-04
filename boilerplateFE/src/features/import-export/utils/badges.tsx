import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import type { ImportJob } from '@/types';

export function StatusBadge({ job }: { job: ImportJob }) {
  const { t } = useTranslation();
  const { status, processedRows, totalRows } = job;

  switch (status) {
    case 'Pending':
      return (
        <Badge variant="secondary" className="gap-1">
          <Spinner size="sm" className="h-3 w-3" />
          {t('importExport.pending', 'Pending')}
        </Badge>
      );
    case 'Validating':
      return (
        <Badge variant="default" className="gap-1">
          <Spinner size="sm" className="h-3 w-3" />
          {t('importExport.validating', 'Validating')}
        </Badge>
      );
    case 'Processing': {
      const pct = totalRows > 0 ? Math.round((processedRows / totalRows) * 100) : 0;
      return (
        <Badge variant="default" className="gap-1">
          <Spinner size="sm" className="h-3 w-3" />
          {t('importExport.processing', 'Processing')} {pct}%
        </Badge>
      );
    }
    case 'Completed':
      return (
        <Badge variant="outline" className="border-green-500 text-green-600">
          {t('importExport.completed', 'Completed')}
        </Badge>
      );
    case 'PartialSuccess':
      return (
        <Badge variant="outline" className="border-amber-500 text-amber-600">
          {t('importExport.partialSuccess', 'Partial Success')}
        </Badge>
      );
    case 'Failed':
      return <Badge variant="destructive">{t('importExport.failed')}</Badge>;
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}
