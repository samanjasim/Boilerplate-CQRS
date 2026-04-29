import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
import { FileUpload } from '@/components/common';
import { useUploadFile } from '../api';
import { FILE_CATEGORIES, getFileCategoryLabel } from '../utils/file-display';
import type { FileCategory } from '@/types';

export interface FileUploadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  defaultCategory?: FileCategory;
}

export function FileUploadDialog({
  open,
  onOpenChange,
  defaultCategory = 'Document',
}: FileUploadDialogProps) {
  const { t } = useTranslation();
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadCategory, setUploadCategory] = useState<FileCategory>(defaultCategory);
  const [uploadDescription, setUploadDescription] = useState('');
  const [uploadTags, setUploadTags] = useState('');
  const [uploadIsPublic, setUploadIsPublic] = useState(false);
  const { mutate: doUpload, isPending: isUploading } = useUploadFile();

  const resetForm = useCallback(() => {
    setUploadFile(null);
    setUploadCategory(defaultCategory);
    setUploadDescription('');
    setUploadTags('');
    setUploadIsPublic(false);
  }, [defaultCategory]);

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
          onOpenChange(false);
          resetForm();
        },
      }
    );
  }, [
    doUpload,
    onOpenChange,
    resetForm,
    uploadCategory,
    uploadDescription,
    uploadFile,
    uploadIsPublic,
    uploadTags,
  ]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('files.uploadFiles')}</DialogTitle>
          <DialogDescription>{t('files.dragDrop')}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <FileUpload onUpload={(file) => setUploadFile(file)} disabled={isUploading} />
          <div className="space-y-2">
            <Label>{t('files.category')}</Label>
            <Select
              value={uploadCategory}
              onValueChange={(value) => setUploadCategory(value as FileCategory)}
            >
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
              value={uploadDescription}
              onChange={(event) => setUploadDescription(event.target.value)}
              placeholder={t('files.description')}
            />
          </div>
          <div className="space-y-2">
            <Label>{t('files.tags')}</Label>
            <Input
              value={uploadTags}
              onChange={(event) => setUploadTags(event.target.value)}
              placeholder={t('files.tagsPlaceholder')}
            />
          </div>
          <div className="flex items-center gap-3">
            <input
              type="checkbox"
              id="upload-public"
              checked={uploadIsPublic}
              onChange={(event) => setUploadIsPublic(event.target.checked)}
              className="h-4 w-4 rounded border-gray-300"
            />
            <div>
              <Label htmlFor="upload-public">{t('files.isPublic')}</Label>
              <p className="text-xs text-muted-foreground">{t('files.isPublicDesc')}</p>
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isUploading}>
            {t('common.cancel')}
          </Button>
          <Button onClick={handleUploadSubmit} disabled={!uploadFile || isUploading}>
            {isUploading ? t('common.loading') : t('files.upload')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
