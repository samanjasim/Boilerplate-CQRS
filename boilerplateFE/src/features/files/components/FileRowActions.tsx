import { useCallback, useMemo, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Copy,
  Download,
  MoreVertical,
  Pencil,
  Share2,
  Trash2,
  UserCog,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  ConfirmDialog,
  OwnershipTransferDialog,
  ResourceShareDialog,
} from '@/components/common';
import { PERMISSIONS } from '@/constants';
import { usePermissions } from '@/hooks';
import { useAuthStore } from '@/stores/auth.store';
import { useDeleteFile } from '../api';
import type { ResourceVisibility } from '@/features/access/types';
import type { FileMetadata } from '@/types';

export interface FileRowActionsProps {
  file: FileMetadata;
  onDeleted?: (fileId: string) => void;
  onDownload?: (file: FileMetadata) => void | Promise<void>;
  onCopyUrl?: (file: FileMetadata) => void | Promise<void>;
  onEdit?: (file: FileMetadata) => void;
  trigger?: 'icon' | 'inline';
}

export function FileRowActions({
  file,
  onDeleted,
  onDownload,
  onCopyUrl,
  onEdit,
  trigger = 'icon',
}: FileRowActionsProps) {
  const { t } = useTranslation();
  const currentUser = useAuthStore((state) => state.user);
  const { hasPermission } = usePermissions();
  const { mutate: doDelete, isPending: isDeleting } = useDeleteFile();
  const [shareOpen, setShareOpen] = useState(false);
  const [transferOpen, setTransferOpen] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState(false);

  const canDelete = hasPermission(PERMISSIONS.Files.Delete);
  const canManage = hasPermission(PERMISSIONS.Files.Manage);
  const canShare = hasPermission(PERMISSIONS.Files.ShareOwn) || canManage;
  const isOwner = currentUser?.id === file.uploadedBy;
  const fileCanShare = canShare || isOwner;
  const canEdit = !!onEdit && canManage;
  const hasMenuItems = useMemo(
    () => fileCanShare || isOwner || !!onDownload || !!onCopyUrl || canEdit || canDelete,
    [canDelete, canEdit, fileCanShare, isOwner, onCopyUrl, onDownload]
  );

  const handleDeleteConfirm = useCallback(() => {
    doDelete(file.id, {
      onSuccess: () => {
        setDeleteConfirm(false);
        onDeleted?.(file.id);
      },
    });
  }, [doDelete, file.id, onDeleted]);

  const stopPropagation = (event: MouseEvent) => {
    event.stopPropagation();
  };

  if (!hasMenuItems) return null;

  return (
    <div onClick={stopPropagation}>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant={trigger === 'icon' ? 'ghost' : 'outline'}
            size={trigger === 'icon' ? 'icon' : 'sm'}
            className={trigger === 'icon' ? 'h-8 w-8' : undefined}
            aria-label={t('common.actions')}
          >
            <MoreVertical className="h-4 w-4" />
            {trigger === 'inline' && t('common.actions')}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {fileCanShare && (
            <DropdownMenuItem onClick={() => setShareOpen(true)}>
              <Share2 className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('access.share')}
            </DropdownMenuItem>
          )}
          {isOwner && (
            <DropdownMenuItem onClick={() => setTransferOpen(true)}>
              <UserCog className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('access.transferOwnership.action')}
            </DropdownMenuItem>
          )}
          {onDownload && (
            <DropdownMenuItem onClick={() => void onDownload(file)}>
              <Download className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('files.download')}
            </DropdownMenuItem>
          )}
          {onCopyUrl && (
            <DropdownMenuItem onClick={() => void onCopyUrl(file)}>
              <Copy className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('files.copyUrl')}
            </DropdownMenuItem>
          )}
          {canEdit && (
            <DropdownMenuItem onClick={() => onEdit?.(file)}>
              <Pencil className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('common.edit')}
            </DropdownMenuItem>
          )}
          {canDelete && (
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              onClick={() => setDeleteConfirm(true)}
            >
              <Trash2 className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('common.delete')}
            </DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      <ConfirmDialog
        isOpen={deleteConfirm}
        onClose={() => setDeleteConfirm(false)}
        onConfirm={handleDeleteConfirm}
        title={t('files.deleteFile')}
        description={t('files.deleteConfirm', { name: file.fileName })}
        isLoading={isDeleting}
      />

      <ResourceShareDialog
        open={shareOpen}
        onOpenChange={setShareOpen}
        resourceType="File"
        resourceId={file.id}
        resourceName={file.fileName}
        currentVisibility={file.visibility as ResourceVisibility}
        fileId={file.id}
      />

      {currentUser && (
        <OwnershipTransferDialog
          open={transferOpen}
          onOpenChange={setTransferOpen}
          resourceType="File"
          resourceId={file.id}
          resourceName={file.fileName}
          currentOwnerId={currentUser.id}
        />
      )}
    </div>
  );
}
