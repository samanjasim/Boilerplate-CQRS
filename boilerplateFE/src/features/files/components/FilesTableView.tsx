import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { VisibilityBadge } from '@/components/common';
import { useResourceGrants } from '@/features/access/api/access.queries';
import { formatDate } from '@/utils/format';
import { formatFileSize } from '@/utils';
import { FileRowActions } from './FileRowActions';
import { getFileCategoryLabel, renderFileIcon } from '../utils/file-display';
import type { ResourceVisibility } from '@/features/access/types';
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

function SharedWithCell({ fileId }: { fileId: string }) {
  const { data: grants = [] } = useResourceGrants('File', fileId);
  if (grants.length === 0) return <span className="text-xs text-muted-foreground">-</span>;
  return <span className="text-xs text-muted-foreground">{grants.length}</span>;
}

export function FilesTableView({
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
            {files.map((file) => (
                <TableRow key={file.id}>
                  <TableCell>
                    <div
                      className="flex cursor-pointer items-center gap-2 hover:text-primary"
                      onClick={() => onSelect(file)}
                    >
                      {renderFileIcon(file, 'h-4 w-4 shrink-0 text-muted-foreground')}
                      <span className="max-w-[200px] truncate font-medium">
                        {file.fileName}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <Badge variant="secondary">{getFileCategoryLabel(file.category, t)}</Badge>
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
                    <FileRowActions
                      file={file}
                      onDeleted={onDeleted}
                      onDownload={onDownload}
                      onCopyUrl={onCopyUrl}
                      onEdit={onEdit}
                    />
                  </TableCell>
                </TableRow>
              ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
