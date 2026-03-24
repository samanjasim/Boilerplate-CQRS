import { useState, useRef, useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload, X, Loader2, Check } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { formatFileSize } from '@/utils';
import { filesApi } from '@/features/files/api';
import { toast } from 'sonner';

interface FileUploadProps {
  onUpload?: (file: File) => void;
  accept?: string;
  maxSize?: number;
  disabled?: boolean;
  className?: string;
  mode?: 'managed' | 'temp';
  onUploaded?: (fileId: string) => void;
  onFileSelected?: (file: File) => void;
}

export function FileUpload({
  onUpload,
  accept,
  maxSize = 50 * 1024 * 1024,
  disabled = false,
  className,
  mode = 'managed',
  onUploaded,
  onFileSelected,
}: FileUploadProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadComplete, setUploadComplete] = useState(false);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);

  useEffect(() => () => {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
  }, [previewUrl]);

  const validateAndSetFile = useCallback(
    async (file: File) => {
      setError(null);
      setUploadComplete(false);
      if (file.size > maxSize) {
        setError(t('files.fileTooLarge', { size: formatFileSize(maxSize) }));
        setSelectedFile(null);
        return;
      }

      // MIME type validation
      if (accept && file) {
        const acceptTypes = accept.split(',').map(a => a.trim());
        const fileTypeMatch = acceptTypes.some(type => {
          if (type.startsWith('.')) return file.name.toLowerCase().endsWith(type);
          if (type.endsWith('/*')) return file.type.startsWith(type.replace('/*', '/'));
          return file.type === type;
        });
        if (!fileTypeMatch) {
          toast.error(t('files.invalidFileType'));
          return;
        }
      }

      setSelectedFile(file);

      if (mode === 'temp') {
        onFileSelected?.(file);
        setIsUploading(true);
        try {
          const fileMetadata = await filesApi.uploadTemp(file);
          setUploadComplete(true);
          if (file.type.startsWith('image/')) {
            setPreviewUrl(URL.createObjectURL(file));
          }
          onUploaded?.(fileMetadata.id);
        } catch {
          setError(t('common.somethingWentWrong'));
          toast.error(t('common.somethingWentWrong'));
          setSelectedFile(null);
        } finally {
          setIsUploading(false);
        }
      } else {
        onUpload?.(file);
      }
    },
    [maxSize, accept, onUpload, onUploaded, onFileSelected, mode, t]
  );

  const handleDragOver = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      if (!disabled) setIsDragging(true);
    },
    [disabled]
  );

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragging(false);
      if (disabled) return;
      const file = e.dataTransfer.files[0];
      if (file) validateAndSetFile(file);
    },
    [disabled, validateAndSetFile]
  );

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) validateAndSetFile(file);
      if (inputRef.current) inputRef.current.value = '';
    },
    [validateAndSetFile]
  );

  const handleClear = useCallback(() => {
    setSelectedFile(null);
    setError(null);
    setUploadComplete(false);
    setPreviewUrl(null);
  }, []);

  return (
    <div className={cn('space-y-2', className)}>
      <div
        className={cn(
          'flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-6 transition-colors cursor-pointer',
          isDragging && 'border-primary bg-primary/5',
          !isDragging && 'border-muted-foreground/25 hover:border-muted-foreground/50',
          (disabled || isUploading) && 'opacity-50 cursor-not-allowed'
        )}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => !disabled && !isUploading && inputRef.current?.click()}
      >
        <Upload className="h-8 w-8 text-muted-foreground mb-2" />
        <p className="text-sm text-muted-foreground text-center">
          {t('files.dragDrop')}
        </p>
        <p className="text-xs text-muted-foreground mt-1">
          {t('files.maxSize', { size: formatFileSize(maxSize) })}
        </p>
        <input
          ref={inputRef}
          type="file"
          className="hidden"
          accept={accept}
          onChange={handleChange}
          disabled={disabled || isUploading}
        />
      </div>

      {error && (
        <p className="text-sm text-destructive">{error}</p>
      )}

      {selectedFile && !error && (
        <div className="flex items-center justify-between rounded-md border p-3">
          <div className="flex items-center gap-3 min-w-0 flex-1">
            {uploadComplete && previewUrl && (
              <img
                src={previewUrl}
                alt={selectedFile.name}
                className="h-10 w-10 rounded border object-cover shrink-0"
              />
            )}
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium truncate">{selectedFile.name}</p>
              <p className="text-xs text-muted-foreground">
                {isUploading
                  ? t('files.uploading')
                  : uploadComplete
                    ? t('files.uploadComplete')
                    : formatFileSize(selectedFile.size)}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1">
            {isUploading && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
            {uploadComplete && <Check className="h-4 w-4 text-green-500" />}
            {!isUploading && (
              <Button variant="ghost" size="sm" onClick={handleClear}>
                <X className="h-4 w-4" />
              </Button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
