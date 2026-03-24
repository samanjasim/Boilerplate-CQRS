import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Download } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useRequestReport } from '@/features/reports/api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';

interface ExportButtonProps {
  reportType: string;
  filters?: Record<string, unknown>;
  canForceRefresh?: boolean;
}

export function ExportButton({ reportType, filters, canForceRefresh }: ExportButtonProps) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const showForceRefresh = canForceRefresh && hasPermission(PERMISSIONS.System.ForceExport);
  const [forceRefresh, setForceRefresh] = useState(false);
  const { mutate: requestReport, isPending } = useRequestReport();

  const handleExport = (format: 'Csv' | 'Pdf') => {
    requestReport({
      reportType,
      format,
      filters: filters ? JSON.stringify(filters) : undefined,
      forceRefresh: forceRefresh || undefined,
    });
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" disabled={isPending}>
          <Download className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
          {isPending ? t('common.exporting') : t('common.export')}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => handleExport('Csv')}>
          {t('common.exportCsv')}
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => handleExport('Pdf')}>
          {t('common.exportPdf')}
        </DropdownMenuItem>
        {showForceRefresh && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={(e) => {
                e.preventDefault();
                setForceRefresh((prev) => !prev);
              }}
            >
              <input
                type="checkbox"
                checked={forceRefresh}
                onChange={() => setForceRefresh((prev) => !prev)}
                className="h-3 w-3 rounded border-gray-300 ltr:mr-2 rtl:ml-2"
              />
              {t('reports.forceRefresh')}
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
