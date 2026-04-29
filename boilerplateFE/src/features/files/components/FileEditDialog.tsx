import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useUpdateFile } from '../api';
import { FILE_CATEGORIES, getFileCategoryLabel } from '../utils/file-display';
import type { FileMetadata } from '@/types';

export interface FileEditDialogProps {
  file: FileMetadata | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: (file: FileMetadata) => void;
}

function FileEditForm({
  file,
  onOpenChange,
  onSaved,
}: {
  file: FileMetadata;
  onOpenChange: (open: boolean) => void;
  onSaved?: (file: FileMetadata) => void;
}) {
  const { t } = useTranslation();
  const [editDescription, setEditDescription] = useState(file.description ?? '');
  const [editCategory, setEditCategory] = useState(file.category);
  const [editTags, setEditTags] = useState(file.tags?.join(', ') ?? '');
  const { mutate: doUpdate, isPending: isUpdating } = useUpdateFile();

  const handleEditSave = useCallback(() => {
    const updatedFile: FileMetadata = {
      ...file,
      category: editCategory,
      description: editDescription,
      tags: editTags
        ? editTags.split(',').map((tag) => tag.trim()).filter(Boolean)
        : null,
    };

    doUpdate(
      {
        id: file.id,
        data: {
          description: editDescription,
          category: editCategory,
          tags: editTags,
        },
      },
      {
        onSuccess: () => {
          onOpenChange(false);
          onSaved?.(updatedFile);
        },
        onError: () => {
          /* keep edit form open; error toast handled by global interceptor */
        },
      }
    );
  }, [doUpdate, editCategory, editDescription, editTags, file, onOpenChange, onSaved]);

  return (
    <>
      <div className="space-y-3">
        <div className="space-y-2">
          <Label>{t('files.category')}</Label>
          <Select value={editCategory} onValueChange={setEditCategory}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {FILE_CATEGORIES.map((category) => (
                <SelectItem key={category} value={category}>
                  {getFileCategoryLabel(category, t)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t('files.description')}</Label>
          <Textarea
            value={editDescription}
            onChange={(event) => setEditDescription(event.target.value)}
          />
        </div>
        <div className="space-y-2">
          <Label>{t('files.tags')}</Label>
          <Input
            value={editTags}
            onChange={(event) => setEditTags(event.target.value)}
            placeholder={t('files.tagsPlaceholder')}
          />
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isUpdating}>
          {t('common.cancel')}
        </Button>
        <Button onClick={handleEditSave} disabled={isUpdating}>
          {isUpdating ? t('common.saving') : t('common.save')}
        </Button>
      </DialogFooter>
    </>
  );
}

export function FileEditDialog({ file, open, onOpenChange, onSaved }: FileEditDialogProps) {
  const { t } = useTranslation();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('common.edit')}</DialogTitle>
        </DialogHeader>
        {file && (
          <FileEditForm
            key={`${file.id}-${open ? 'open' : 'closed'}`}
            file={file}
            onOpenChange={onOpenChange}
            onSaved={onSaved}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}
