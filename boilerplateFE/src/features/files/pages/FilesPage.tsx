import { useState, useMemo, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDate, formatDateTime } from '@/utils/format';
import {
  Upload,
  LayoutGrid,
  List,
  FileText,
  Image,
  File as FileIcon,
  Download,
  Trash2,
  Copy,
  Pencil,
  FolderOpen,
  MoreVertical,
  Share2,
  UserCog,
} from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  PageHeader,
  EmptyState,
  FileUpload,
  ConfirmDialog,
  ExportButton,
  Pagination,
  getPersistedPageSize,
  VisibilityBadge,
  ResourceShareDialog,
  OwnershipTransferDialog,
} from '@/components/common';
import { useResourceGrants } from '@/features/access/api/access.queries';
import { StorageSummaryPanel } from '../components/StorageSummaryPanel';
import { filesApi, useFiles, useUploadFile, useDeleteFile, useUpdateFile } from '../api';
import { toast } from 'sonner';
import { usePermissions } from '@/hooks';
import { useAuthStore } from '@/stores/auth.store';
import { PERMISSIONS } from '@/constants';
import { formatFileSize } from '@/utils';
import { cn } from '@/lib/utils';
import type { FileMetadata, FileCategory } from '@/types';
import type { ResourceVisibility } from '@/features/access/types';

type ViewFilter = 'all' | 'mine' | 'shared' | 'public';

function SharedWithCell({ fileId }: { fileId: string }) {
  const { data: grants = [] } = useResourceGrants('File', fileId);
  if (grants.length === 0) return <span className="text-muted-foreground text-xs">—</span>;
  return (
    <span className="text-xs text-muted-foreground">
      {grants.length}
    </span>
  );
}

function isImageType(contentType: string): boolean {
  return contentType.startsWith('image/');
}

function getFileIcon(contentType: string) {
  if (isImageType(contentType)) return Image;
  if (contentType.includes('pdf') || contentType.includes('document') || contentType.includes('text'))
    return FileText;
  return FileIcon;
}

const CATEGORIES: FileCategory[] = ['Avatar', 'Logo', 'Document', 'Attachment', 'Other'];

export default function FilesPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const currentUser = useAuthStore(s => s.user);
  const canUpload = hasPermission(PERMISSIONS.Files.Upload);
  const canDelete = hasPermission(PERMISSIONS.Files.Delete);
  const canManage = hasPermission(PERMISSIONS.Files.Manage);
  const canShare = hasPermission(PERMISSIONS.Files.ShareOwn) || canManage;
  const canExport = hasPermission(PERMISSIONS.System.ExportData);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [view, setView] = useState<ViewFilter>('all');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [category, setCategory] = useState<string>('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [origin, setOrigin] = useState<string>('');
  const [uploadDialogOpen, setUploadDialogOpen] = useState(false);
  const [detailFile, setDetailFile] = useState<FileMetadata | null>(null);
  const [deleteFile, setDeleteFile] = useState<FileMetadata | null>(null);
  const [shareFile, setShareFile] = useState<FileMetadata | null>(null);
  const [transferFile, setTransferFile] = useState<FileMetadata | null>(null);
  const [isEditing, setIsEditing] = useState(false);

  // Upload form state
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadCategory, setUploadCategory] = useState<FileCategory>('Document');
  const [uploadDescription, setUploadDescription] = useState('');
  const [uploadTags, setUploadTags] = useState('');
  const [uploadIsPublic, setUploadIsPublic] = useState(false);

  // Edit form state
  const [editDescription, setEditDescription] = useState('');
  const [editCategory, setEditCategory] = useState('');
  const [editTags, setEditTags] = useState('');

  const params = useMemo(() => {
    const p: Record<string, unknown> = { pageNumber, pageSize };
    if (category && category !== 'all') p.category = category;
    if (searchTerm) p.searchTerm = searchTerm;
    if (origin) p.origin = origin;
    if (view !== 'all') p.view = view;
    return p;
  }, [pageNumber, pageSize, category, searchTerm, origin, view]);

  const { data, isLoading, isFetching, isError } = useFiles(params);
  const { mutate: doUpload, isPending: isUploading } = useUploadFile();
  const { mutate: doDelete, isPending: isDeleting } = useDeleteFile();
  const { mutate: doUpdate, isPending: isUpdating } = useUpdateFile();

  const files: FileMetadata[] = data?.data ?? [];
  const pagination = data?.pagination;

  const handleUploadSubmit = useCallback(() => {
    if (!uploadFile) return;
    doUpload(
      {
        file: uploadFile,
        category: uploadCategory,
        description: uploadDescription || undefined,
        tags: uploadTags || undefined,
        visibility: uploadIsPublic ? 'Public' : 'Private',
      },
      {
        onSuccess: () => {
          setUploadDialogOpen(false);
          setUploadFile(null);
          setUploadDescription('');
          setUploadTags('');
          setUploadIsPublic(false);
          setUploadCategory('Document');
        },
      }
    );
  }, [uploadFile, uploadCategory, uploadDescription, uploadTags, uploadIsPublic, doUpload]);

  const handleDeleteConfirm = useCallback(() => {
    if (!deleteFile) return;
    doDelete(deleteFile.id, {
      onSuccess: () => {
        setDeleteFile(null);
        if (detailFile?.id === deleteFile.id) setDetailFile(null);
      },
    });
  }, [deleteFile, detailFile, doDelete]);

  const handleEditSave = useCallback(() => {
    if (!detailFile) return;
    doUpdate(
      {
        id: detailFile.id,
        data: {
          description: editDescription,
          category: editCategory,
          tags: editTags,
        },
      },
      {
        onSuccess: () => {
          setIsEditing(false);
          setDetailFile(null);
        },
        onError: () => { /* keep edit form open; error toast handled by global interceptor */ },
      }
    );
  }, [detailFile, editDescription, editCategory, editTags, doUpdate]);

  const openDetail = useCallback((file: FileMetadata) => {
    setDetailFile(file);
    setIsEditing(false);
    setEditDescription(file.description ?? '');
    setEditCategory(file.category);
    setEditTags(file.tags?.join(', ') ?? '');
  }, []);

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
    [t, resolveUrl]
  );

  const handleDownload = useCallback(async (file: FileMetadata) => {
    const url = await resolveUrl(file);
    if (url) {
      window.open(url, '_blank');
    }
  }, [resolveUrl]);

  const exportFilters = useMemo(() => {
    const f: Record<string, unknown> = {};
    if (category && category !== 'all') f.category = category;
    if (searchTerm) f.searchTerm = searchTerm;
    if (origin) f.origin = origin;
    return f;
  }, [category, searchTerm, origin]);

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
      {/* Header */}
      <PageHeader
        title={t('files.title')}
        actions={
          <div className="flex items-center gap-2">
            <div className="flex rounded-xl bg-secondary p-0.5 gap-0.5">
              <Button
                variant={viewMode === 'grid' ? 'default' : 'ghost'}
                size="sm"
                onClick={() => setViewMode('grid')}
                title={t('files.gridView')}
              >
                <LayoutGrid className="h-4 w-4" />
              </Button>
              <Button
                variant={viewMode === 'list' ? 'default' : 'ghost'}
                size="sm"
                onClick={() => setViewMode('list')}
                title={t('files.listView')}
              >
                <List className="h-4 w-4" />
              </Button>
            </div>
            <StorageSummaryPanel />
            {canExport && <ExportButton reportType="Files" filters={exportFilters} />}
            {canUpload && (
              <Button onClick={() => setUploadDialogOpen(true)}>
                <Upload className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                {t('files.upload')}
              </Button>
            )}
          </div>
        }
      />

      {/* View tabs */}
      <div className="flex gap-1 border-b">
        {(['all', 'mine', 'shared', 'public'] as const).map(v => (
          <button
            key={v}
            type="button"
            className={cn(
              'px-4 py-2 text-sm -mb-px border-b-2 transition-colors',
              view === v
                ? 'border-primary text-foreground font-medium'
                : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
            onClick={() => { setView(v); setPageNumber(1); }}
          >
            {t(`files.views.${v}`)}
          </button>
        ))}
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="py-4">
          <div className="flex flex-wrap items-center gap-4">
            <div className="w-48">
              <Select
                value={category}
                onValueChange={(v) => {
                  setCategory(v);
                  setPageNumber(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t('files.category')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('files.allCategories')}</SelectItem>
                  {CATEGORIES.map((cat) => (
                    <SelectItem key={cat} value={cat}>
                      {t(`files.${cat.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="w-48">
              <Select
                value={origin || 'all'}
                onValueChange={(v) => {
                  setOrigin(v === 'all' ? '' : v);
                  setPageNumber(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t('files.allFiles')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('files.allFiles')}</SelectItem>
                  <SelectItem value="UserUpload">{t('files.myFiles')}</SelectItem>
                  <SelectItem value="SystemGenerated">{t('files.systemFiles')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex-1 min-w-[200px]">
              <Input
                placeholder={t('common.search')}
                value={searchTerm}
                onChange={(e) => {
                  setSearchTerm(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Content */}
      <div className={`relative ${isFetching && !isLoading ? 'opacity-60 pointer-events-none' : ''}`}>
      {isFetching && !isLoading && (
        <div className="absolute inset-0 z-10 flex items-start justify-center pt-12">
          <Spinner size="md" />
        </div>
      )}
      {files.length === 0 && !isFetching ? (
        <EmptyState icon={FolderOpen} title={t('files.noFiles')} />
      ) : viewMode === 'grid' ? (
        /* Grid View */
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
          {files.map((file) => {
            const Icon = getFileIcon(file.contentType);
            return (
              <Card
                key={file.id}
                className="cursor-pointer transition-shadow hover:shadow-md"
                onClick={() => openDetail(file)}
              >
                <CardContent className="p-4">
                  <div className="flex h-32 items-center justify-center rounded-md bg-muted mb-3">
                    {isImageType(file.contentType) && file.url ? (
                      <img
                        src={file.url}
                        alt={file.fileName}
                        className="h-full w-full rounded-md object-cover"
                        onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                      />
                    ) : (
                      <Icon className="h-12 w-12 text-muted-foreground" />
                    )}
                  </div>
                  <p className="text-sm font-medium truncate" title={file.fileName}>
                    {file.fileName}
                  </p>
                  <div className="mt-1 flex items-center justify-between">
                    <span className="text-xs text-muted-foreground">
                      {formatFileSize(file.size)}
                    </span>
                    <div className="flex items-center gap-1">
                      <Badge variant="secondary" className="text-xs">
                        {file.category}
                      </Badge>
                      {canDelete && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-muted-foreground hover:text-destructive"
                          onClick={(e) => {
                            e.stopPropagation();
                            setDeleteFile(file);
                          }}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {formatDate(file.createdAt)}
                  </p>
                </CardContent>
              </Card>
            );
          })}
        </div>
      ) : (
        /* List View */
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('files.fileName')}</TableHead>
                  <TableHead>{t('files.category')}</TableHead>
                  <TableHead>{t('files.fileSize')}</TableHead>
                  <TableHead>{t('access.visibility.label')}</TableHead>
                  <TableHead>{t('access.sharedWith')}</TableHead>
                  <TableHead>{t('files.uploadedBy')}</TableHead>
                  <TableHead>{t('files.uploadDate')}</TableHead>
                  <TableHead>{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {files.map((file) => {
                  const Icon = getFileIcon(file.contentType);
                  const isOwner = currentUser?.id === file.uploadedBy;
                  const fileCanShare = canShare || isOwner;
                  return (
                    <TableRow key={file.id}>
                      <TableCell>
                        <div
                          className="flex items-center gap-2 cursor-pointer hover:text-primary"
                          onClick={() => openDetail(file)}
                        >
                          <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
                          <span className="font-medium truncate max-w-[200px]">
                            {file.fileName}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant="secondary">{file.category}</Badge>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {formatFileSize(file.size)}
                      </TableCell>
                      <TableCell>
                        <VisibilityBadge visibility={file.visibility as ResourceVisibility} />
                      </TableCell>
                      <TableCell>
                        <SharedWithCell fileId={file.id} />
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {file.uploadedByName ?? '-'}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {formatDate(file.createdAt)}
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8"
                              aria-label={t('common.actions')}
                            >
                              <MoreVertical className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            {fileCanShare && (
                              <DropdownMenuItem onClick={() => setShareFile(file)}>
                                <Share2 className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                                {t('access.share')}
                              </DropdownMenuItem>
                            )}
                            {isOwner && (
                              <DropdownMenuItem onClick={() => setTransferFile(file)}>
                                <UserCog className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                                {t('access.transferOwnership.action')}
                              </DropdownMenuItem>
                            )}
                            <DropdownMenuItem onClick={() => handleDownload(file)}>
                              <Download className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                              {t('files.download')}
                            </DropdownMenuItem>
                            {canDelete && (
                              <DropdownMenuItem
                                className="text-destructive focus:text-destructive"
                                onClick={() => setDeleteFile(file)}
                              >
                                <Trash2 className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                                {t('common.delete')}
                              </DropdownMenuItem>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Pagination */}
      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}
      </div>

      {/* Upload Dialog */}
      <Dialog open={uploadDialogOpen} onOpenChange={setUploadDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('files.uploadFiles')}</DialogTitle>
            <DialogDescription>{t('files.dragDrop')}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <FileUpload
              onUpload={(file) => setUploadFile(file)}
              disabled={isUploading}
            />
            <div className="space-y-2">
              <Label>{t('files.category')}</Label>
              <Select
                value={uploadCategory}
                onValueChange={(v) => setUploadCategory(v as FileCategory)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CATEGORIES.map((cat) => (
                    <SelectItem key={cat} value={cat}>
                      {t(`files.${cat.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>{t('files.description')}</Label>
              <Textarea
                value={uploadDescription}
                onChange={(e) => setUploadDescription(e.target.value)}
                placeholder={t('files.description')}
              />
            </div>
            <div className="space-y-2">
              <Label>{t('files.tags')}</Label>
              <Input
                value={uploadTags}
                onChange={(e) => setUploadTags(e.target.value)}
                placeholder={t('files.tagsPlaceholder')}
              />
            </div>
            <div className="flex items-center gap-3">
              <input
                type="checkbox"
                id="upload-public"
                checked={uploadIsPublic}
                onChange={(e) => setUploadIsPublic(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300"
              />
              <div>
                <Label htmlFor="upload-public">{t('files.isPublic')}</Label>
                <p className="text-xs text-muted-foreground">{t('files.isPublicDesc')}</p>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setUploadDialogOpen(false)}
              disabled={isUploading}
            >
              {t('common.cancel')}
            </Button>
            <Button onClick={handleUploadSubmit} disabled={!uploadFile || isUploading}>
              {isUploading ? t('common.loading') : t('files.upload')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* File Detail Modal */}
      <Dialog open={!!detailFile} onOpenChange={(open) => !open && setDetailFile(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('files.fileDetails')}</DialogTitle>
          </DialogHeader>
          {detailFile && (
            <div className="space-y-4">
              {/* Preview */}
              <div className="flex h-48 items-center justify-center rounded-md bg-muted">
                {isImageType(detailFile.contentType) && detailFile.url ? (
                  <img
                    src={detailFile.url}
                    alt={detailFile.fileName}
                    className="h-full w-full rounded-md object-contain"
                    onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
                  />
                ) : (
                  (() => {
                    const Icon = getFileIcon(detailFile.contentType);
                    return <Icon className="h-16 w-16 text-muted-foreground" />;
                  })()
                )}
              </div>

              {isEditing ? (
                /* Edit Mode */
                <div className="space-y-3">
                  <div className="space-y-2">
                    <Label>{t('files.category')}</Label>
                    <Select value={editCategory} onValueChange={setEditCategory}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {CATEGORIES.map((cat) => (
                          <SelectItem key={cat} value={cat}>
                            {t(`files.${cat.toLowerCase()}`)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>{t('files.description')}</Label>
                    <Textarea
                      value={editDescription}
                      onChange={(e) => setEditDescription(e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>{t('files.tags')}</Label>
                    <Input
                      value={editTags}
                      onChange={(e) => setEditTags(e.target.value)}
                      placeholder={t('files.tagsPlaceholder')}
                    />
                  </div>
                </div>
              ) : (
                /* View Mode */
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">{t('files.fileName')}</span>
                    <span className="font-medium truncate max-w-[250px]" title={detailFile.fileName}>
                      {detailFile.fileName}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">{t('files.fileSize')}</span>
                    <span>{formatFileSize(detailFile.size)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">{t('files.fileType')}</span>
                    <span>{detailFile.contentType}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">{t('files.category')}</span>
                    <Badge variant="secondary">{detailFile.category}</Badge>
                  </div>
                  {detailFile.tags && detailFile.tags.length > 0 && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">{t('files.tags')}</span>
                      <div className="flex flex-wrap gap-1 justify-end">
                        {detailFile.tags.map((tag) => (
                          <Badge key={tag} variant="outline" className="text-xs">
                            {tag}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}
                  {detailFile.description && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">{t('files.description')}</span>
                      <span className="text-end max-w-[250px]">{detailFile.description}</span>
                    </div>
                  )}
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">{t('files.uploadedBy')}</span>
                    <span>{detailFile.uploadedByName ?? '-'}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">{t('files.uploadDate')}</span>
                    <span>{formatDateTime(detailFile.createdAt)}</span>
                  </div>
                </div>
              )}

              {/* Actions */}
              <div className="flex flex-wrap gap-2">
                {isEditing ? (
                  <>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setIsEditing(false)}
                      disabled={isUpdating}
                    >
                      {t('common.cancel')}
                    </Button>
                    <Button size="sm" onClick={handleEditSave} disabled={isUpdating}>
                      {isUpdating ? t('common.saving') : t('common.save')}
                    </Button>
                  </>
                ) : (
                  <>
                    <Button variant="outline" size="sm" onClick={() => handleDownload(detailFile)}>
                      <Download className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                      {t('files.download')}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleCopyUrl(detailFile)}
                    >
                      <Copy className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                      {t('files.copyUrl')}
                    </Button>
                    {canManage && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setIsEditing(true)}
                      >
                        <Pencil className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                        {t('common.edit')}
                      </Button>
                    )}
                    {canDelete && (
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => setDeleteFile(detailFile)}
                      >
                        <Trash2 className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                        {t('common.delete')}
                      </Button>
                    )}
                  </>
                )}
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation */}
      <ConfirmDialog
        isOpen={!!deleteFile}
        onClose={() => setDeleteFile(null)}
        onConfirm={handleDeleteConfirm}
        title={t('files.deleteFile')}
        description={t('files.deleteConfirm', { name: deleteFile?.fileName ?? '' })}
        isLoading={isDeleting}
      />

      {/* Share Dialog */}
      {shareFile && (
        <ResourceShareDialog
          open={!!shareFile}
          onOpenChange={open => { if (!open) setShareFile(null); }}
          resourceType="File"
          resourceId={shareFile.id}
          resourceName={shareFile.fileName}
          currentVisibility={shareFile.visibility as ResourceVisibility}
          fileId={shareFile.id}
        />
      )}

      {/* Transfer Ownership Dialog */}
      {transferFile && currentUser && (
        <OwnershipTransferDialog
          open={!!transferFile}
          onOpenChange={open => { if (!open) setTransferFile(null); }}
          resourceType="File"
          resourceId={transferFile.id}
          resourceName={transferFile.fileName}
          currentOwnerId={currentUser.id}
        />
      )}
    </div>
  );
}
