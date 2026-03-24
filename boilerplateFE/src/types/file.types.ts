export interface FileMetadata {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
  category: string;
  tags: string[] | null;
  description: string | null;
  entityType: string | null;
  entityId: string | null;
  isPublic: boolean;
  uploadedBy: string;
  uploadedByName: string | null;
  createdAt: string;
  url: string | null;
  status: string;
  origin: string;
  expiresAt: string | null;
}

export type FileCategory = 'Avatar' | 'Logo' | 'Document' | 'Attachment' | 'Other';

export interface UploadFileData {
  file: File;
  category?: FileCategory;
  description?: string;
  tags?: string;
  entityType?: string;
  entityId?: string;
  isPublic?: boolean;
}

export interface UpdateFileData {
  description?: string;
  category?: string;
  tags?: string;
}
