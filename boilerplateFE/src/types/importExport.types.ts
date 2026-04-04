export interface ImportJob {
  id: string;
  entityType: string;
  fileName: string;
  conflictMode: string;
  status: 'Pending' | 'Validating' | 'Processing' | 'Completed' | 'PartialSuccess' | 'Failed';
  totalRows: number;
  processedRows: number;
  createdCount: number;
  updatedCount: number;
  skippedCount: number;
  failedCount: number;
  hasErrorReport: boolean;
  errorMessage: string | null;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
}

export interface ImportPreview {
  headers: string[];
  previewRows: string[][];
  validationErrors: string[];
  totalRowCount: number;
  unrecognizedColumns: string[];
}

export interface EntityType {
  entityType: string;
  displayName: string;
  supportsExport: boolean;
  supportsImport: boolean;
  requiresTenant: boolean;
  fields: string[];
}

export interface StartImportData {
  fileId: string;
  entityType: string;
  conflictMode: number;
  targetTenantId?: string;
}

export interface PreviewImportData {
  fileId: string;
  entityType: string;
}
