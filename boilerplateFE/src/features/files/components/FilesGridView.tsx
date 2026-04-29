import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { formatDate } from '@/utils/format';
import { formatFileSize } from '@/utils';
import { FileRowActions } from './FileRowActions';
import {
  getFileCategoryLabel,
  isImageType,
  renderFileIcon,
} from '../utils/file-display';
import type { FileMetadata } from '@/types';

export interface FilesViewProps {
  files: FileMetadata[];
  isLoading: boolean;
  onSelect: (file: FileMetadata) => void;
  onDeleted?: (fileId: string) => void;
  onEdit?: (file: FileMetadata) => void;
  onDownload?: (file: FileMetadata) => void | Promise<void>;
  onCopyUrl?: (file: FileMetadata) => void | Promise<void>;
}

export function FilesGridView({
  files,
  isLoading,
  onSelect,
  onDeleted,
  onEdit,
  onDownload,
  onCopyUrl,
}: FilesViewProps) {
  const { t } = useTranslation();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
      {files.map((file) => {
        return (
          <Card
            key={file.id}
            className="cursor-pointer transition-shadow hover:shadow-md"
            onClick={() => onSelect(file)}
          >
            <CardContent className="p-4">
              <div className="relative">
                <div className="absolute right-2 top-2 z-10">
                  <FileRowActions
                    file={file}
                    onDeleted={onDeleted}
                    onDownload={onDownload}
                    onCopyUrl={onCopyUrl}
                    onEdit={onEdit}
                  />
                </div>
                <div className="mb-3 flex h-32 items-center justify-center rounded-md bg-muted">
                  {isImageType(file.contentType) && file.url ? (
                    <img
                      src={file.url}
                      alt={file.fileName}
                      className="h-full w-full rounded-md object-cover"
                      onError={(event) => {
                        (event.target as HTMLImageElement).style.display = 'none';
                      }}
                    />
                  ) : (
                    renderFileIcon(file, 'h-12 w-12 text-muted-foreground')
                  )}
                </div>
              </div>
              <p className="truncate text-sm font-medium" title={file.fileName}>
                {file.fileName}
              </p>
              <div className="mt-1 flex items-center justify-between gap-2">
                <span className="text-xs text-muted-foreground">
                  {formatFileSize(file.size)}
                </span>
                <Badge variant="secondary" className="text-xs">
                  {getFileCategoryLabel(file.category, t)}
                </Badge>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {formatDate(file.createdAt)}
              </p>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}
