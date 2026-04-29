import { createElement } from 'react';
import { File as FileIcon, FileText, Image, type LucideIcon } from 'lucide-react';
import type { FileCategory, FileMetadata } from '@/types';

export const FILE_CATEGORIES: FileCategory[] = ['Avatar', 'Logo', 'Document', 'Attachment', 'Other'];

const CATEGORY_LABEL_KEYS: Record<FileCategory, string> = {
  Avatar: 'files.avatar',
  Logo: 'files.logo',
  Document: 'files.document',
  Attachment: 'files.attachment',
  Other: 'files.other',
};

export function isImageType(contentType?: string | null): boolean {
  return contentType?.startsWith('image/') ?? false;
}

export function getFileIcon(file: Pick<FileMetadata, 'contentType'>): LucideIcon {
  const { contentType } = file;
  if (isImageType(contentType)) return Image;
  if (contentType.includes('pdf') || contentType.includes('document') || contentType.includes('text')) {
    return FileText;
  }
  return FileIcon;
}

export function renderFileIcon(file: Pick<FileMetadata, 'contentType'>, className: string) {
  return createElement(getFileIcon(file), { className });
}

export function getFileCategoryLabel(
  category: string | null | undefined,
  t: (key: string) => string
): string {
  if (!category) return t(CATEGORY_LABEL_KEYS.Other);
  return CATEGORY_LABEL_KEYS[category as FileCategory]
    ? t(CATEGORY_LABEL_KEYS[category as FileCategory])
    : category;
}
