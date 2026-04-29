import { LayoutGrid, List, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { ExportButton } from '@/components/common';

export interface FilesPageActionsProps {
  viewMode: 'grid' | 'list';
  onViewModeChange: (viewMode: 'grid' | 'list') => void;
  canExport: boolean;
  exportFilters: Record<string, unknown>;
  canUpload: boolean;
  onUpload: () => void;
}

export function FilesPageActions({
  viewMode,
  onViewModeChange,
  canExport,
  exportFilters,
  canUpload,
  onUpload,
}: FilesPageActionsProps) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center gap-2">
      <div className="flex gap-0.5 rounded-xl bg-secondary p-0.5">
        <Button
          variant={viewMode === 'grid' ? 'default' : 'ghost'}
          size="sm"
          onClick={() => onViewModeChange('grid')}
          title={t('files.gridView')}
        >
          <LayoutGrid className="h-4 w-4" />
        </Button>
        <Button
          variant={viewMode === 'list' ? 'default' : 'ghost'}
          size="sm"
          onClick={() => onViewModeChange('list')}
          title={t('files.listView')}
        >
          <List className="h-4 w-4" />
        </Button>
      </div>
      {canExport && <ExportButton reportType="Files" filters={exportFilters} />}
      {canUpload && (
        <Button onClick={onUpload}>
          <Upload className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
          {t('files.upload')}
        </Button>
      )}
    </div>
  );
}
