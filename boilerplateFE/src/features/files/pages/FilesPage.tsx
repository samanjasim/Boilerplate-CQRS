import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { FolderOpen } from 'lucide-react';
import { Spinner } from '@/components/ui/spinner';
import {
  EmptyState,
  getPersistedPageSize,
  PageHeader,
  Pagination,
} from '@/components/common';
import { PERMISSIONS } from '@/constants';
import { usePermissions } from '@/hooks';
import { cn } from '@/lib/utils';
import { filesApi, useFiles } from '../api';
import { FileDetailDialog } from '../components/FileDetailDialog';
import { FileEditDialog } from '../components/FileEditDialog';
import { FilesFilterPanel, type FilesViewFilter } from '../components/FilesFilterPanel';
import { FileUploadDialog } from '../components/FileUploadDialog';
import { FilesGridView } from '../components/FilesGridView';
import { FilesPageActions } from '../components/FilesPageActions';
import { FilesTableView } from '../components/FilesTableView';
import { StorageHeroStrip } from '../components/StorageHeroStrip';
import type { FileMetadata } from '@/types';

export default function FilesPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canUpload = hasPermission(PERMISSIONS.Files.Upload);
  const canManage = hasPermission(PERMISSIONS.Files.Manage);
  const canExport = hasPermission(PERMISSIONS.System.ExportData);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [view, setView] = useState<FilesViewFilter>('all');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [category, setCategory] = useState('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [origin, setOrigin] = useState('');
  const [uploadDialogOpen, setUploadDialogOpen] = useState(false);
  const [detailFile, setDetailFile] = useState<FileMetadata | null>(null);
  const [editingFile, setEditingFile] = useState<FileMetadata | null>(null);
  const [editOpen, setEditOpen] = useState(false);

  const params = useMemo(() => {
    const nextParams: Record<string, unknown> = { pageNumber, pageSize };
    if (category && category !== 'all') nextParams.category = category;
    if (searchTerm) nextParams.searchTerm = searchTerm;
    if (origin) nextParams.origin = origin;
    if (view !== 'all') nextParams.view = view;
    return nextParams;
  }, [category, origin, pageNumber, pageSize, searchTerm, view]);

  const { data, isLoading, isFetching, isError } = useFiles(params);
  const files: FileMetadata[] = data?.data ?? [];
  const pagination = data?.pagination;

  const resolveUrl = useCallback(async (file: FileMetadata): Promise<string | null> => {
    if (file.url) return file.url;
    try {
      return await filesApi.getFileUrl(file.id);
    } catch {
      return null;
    }
  }, []);

  const handleCopyUrl = useCallback(
    async (file: FileMetadata) => {
      const url = await resolveUrl(file);
      if (!url) {
        toast.error(t('files.noUrlAvailable'));
        return;
      }
      try {
        await navigator.clipboard.writeText(url);
        toast.success(t('files.urlCopied'));
      } catch {
        toast.error(t('common.copyFailed'));
      }
    },
    [resolveUrl, t]
  );

  const handleDownload = useCallback(
    async (file: FileMetadata) => {
      const url = await resolveUrl(file);
      if (url) window.open(url, '_blank');
    },
    [resolveUrl]
  );

  const openEditDialog = useCallback((file: FileMetadata) => {
    setEditingFile(file);
    setEditOpen(true);
  }, []);

  const handleEditOpenChange = useCallback((open: boolean) => {
    setEditOpen(open);
    if (!open) setEditingFile(null);
  }, []);

  const handleEditSaved = useCallback(
    (file: FileMetadata) => {
      if (detailFile?.id === file.id) setDetailFile(null);
    },
    [detailFile?.id]
  );

  const handleDeletedFile = useCallback(
    (fileId: string) => {
      if (detailFile?.id === fileId) setDetailFile(null);
      if (editingFile?.id === fileId) handleEditOpenChange(false);
    },
    [detailFile?.id, editingFile?.id, handleEditOpenChange]
  );

  const exportFilters = useMemo(() => {
    const filters: Record<string, unknown> = {};
    if (category && category !== 'all') filters.category = category;
    if (searchTerm) filters.searchTerm = searchTerm;
    if (origin) filters.origin = origin;
    return filters;
  }, [category, origin, searchTerm]);

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('files.title')} />
        <EmptyState
          icon={FolderOpen}
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
        title={t('files.title')}
        actions={
          <FilesPageActions
            viewMode={viewMode}
            onViewModeChange={setViewMode}
            canExport={canExport}
            exportFilters={exportFilters}
            canUpload={canUpload}
            onUpload={() => setUploadDialogOpen(true)}
          />
        }
      />

      <StorageHeroStrip />

      <FilesFilterPanel
        view={view}
        onViewChange={(nextView) => {
          setView(nextView);
          setPageNumber(1);
        }}
        category={category}
        onCategoryChange={(nextCategory) => {
          setCategory(nextCategory);
          setPageNumber(1);
        }}
        origin={origin}
        onOriginChange={(nextOrigin) => {
          setOrigin(nextOrigin === 'all' ? '' : nextOrigin);
          setPageNumber(1);
        }}
        searchTerm={searchTerm}
        onSearchTermChange={(nextSearchTerm) => {
          setSearchTerm(nextSearchTerm);
          setPageNumber(1);
        }}
      />

      <div className={cn('relative', isFetching && !isLoading && 'pointer-events-none opacity-60')}>
        {isFetching && !isLoading && (
          <div className="absolute inset-0 z-10 flex items-start justify-center pt-12">
            <Spinner size="md" />
          </div>
        )}
        {files.length === 0 && !isFetching ? (
          <EmptyState icon={FolderOpen} title={t('files.noFiles')} />
        ) : viewMode === 'grid' ? (
          <FilesGridView
            files={files}
            isLoading={isLoading}
            onSelect={setDetailFile}
            onDeleted={handleDeletedFile}
            onEdit={openEditDialog}
            onDownload={handleDownload}
            onCopyUrl={handleCopyUrl}
          />
        ) : (
          <FilesTableView
            files={files}
            isLoading={isLoading}
            onSelect={setDetailFile}
            onDeleted={handleDeletedFile}
            onEdit={openEditDialog}
            onDownload={handleDownload}
            onCopyUrl={handleCopyUrl}
          />
        )}

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
      </div>

      <FileUploadDialog open={uploadDialogOpen} onOpenChange={setUploadDialogOpen} />
      <FileEditDialog
        file={editingFile}
        open={editOpen}
        onOpenChange={handleEditOpenChange}
        onSaved={handleEditSaved}
      />

      <FileDetailDialog
        file={detailFile}
        open={!!detailFile}
        onOpenChange={(open) => !open && setDetailFile(null)}
        canManage={canManage}
        onDownload={handleDownload}
        onCopyUrl={handleCopyUrl}
        onEdit={openEditDialog}
        onDeleted={handleDeletedFile}
      />
    </div>
  );
}
