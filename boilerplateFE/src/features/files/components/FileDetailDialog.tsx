import { Copy, Download, Pencil } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { formatFileSize } from '@/utils';
import { formatDateTime } from '@/utils/format';
import { FileRowActions } from './FileRowActions';
import {
  getFileCategoryLabel,
  isImageType,
  renderFileIcon,
} from '../utils/file-display';
import type { FileMetadata } from '@/types';

export interface FileDetailDialogProps {
  file: FileMetadata | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  canManage: boolean;
  onDownload: (file: FileMetadata) => void | Promise<void>;
  onCopyUrl: (file: FileMetadata) => void | Promise<void>;
  onEdit: (file: FileMetadata) => void;
  onDeleted?: (fileId: string) => void;
}

export function FileDetailDialog({
  file,
  open,
  onOpenChange,
  canManage,
  onDownload,
  onCopyUrl,
  onEdit,
  onDeleted,
}: FileDetailDialogProps) {
  const { t } = useTranslation();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('files.fileDetails')}</DialogTitle>
        </DialogHeader>
        {file && (
          <div className="space-y-4">
            <div className="flex h-48 items-center justify-center rounded-md bg-muted">
              {isImageType(file.contentType) && file.url ? (
                <img
                  src={file.url}
                  alt={file.fileName}
                  className="h-full w-full rounded-md object-contain"
                  onError={(event) => {
                    (event.target as HTMLImageElement).style.display = 'none';
                  }}
                />
              ) : renderFileIcon(file, 'h-16 w-16 text-muted-foreground')}
            </div>

            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('files.fileName')}</span>
                <span className="max-w-[250px] truncate font-medium" title={file.fileName}>
                  {file.fileName}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('files.fileSize')}</span>
                <span>{formatFileSize(file.size)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('files.fileType')}</span>
                <span>{file.contentType}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('files.category')}</span>
                <Badge variant="secondary">
                  {getFileCategoryLabel(file.category, t)}
                </Badge>
              </div>
              {file.tags && file.tags.length > 0 && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">{t('files.tags')}</span>
                  <div className="flex flex-wrap justify-end gap-1">
                    {file.tags.map((tag) => (
                      <Badge key={tag} variant="outline" className="text-xs">
                        {tag}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
              {file.description && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">{t('files.description')}</span>
                  <span className="max-w-[250px] text-end">{file.description}</span>
                </div>
              )}
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('files.uploadedBy')}</span>
                <span>{file.uploadedByName ?? '-'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('files.uploadDate')}</span>
                <span>{formatDateTime(file.createdAt)}</span>
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" onClick={() => void onDownload(file)}>
                <Download className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                {t('files.download')}
              </Button>
              <Button variant="outline" size="sm" onClick={() => void onCopyUrl(file)}>
                <Copy className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                {t('files.copyUrl')}
              </Button>
              {canManage && (
                <Button variant="outline" size="sm" onClick={() => onEdit(file)}>
                  <Pencil className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
                  {t('common.edit')}
                </Button>
              )}
              <FileRowActions
                file={file}
                trigger="inline"
                onDeleted={onDeleted}
              />
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
