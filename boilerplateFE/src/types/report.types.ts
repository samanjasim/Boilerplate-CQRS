export interface ReportRequest {
  id: string;
  reportType: string;
  format: string;
  status: string;
  filters: string | null;
  fileName: string | null;
  errorMessage: string | null;
  requestedAt: string;
  completedAt: string | null;
  expiresAt: string | null;
}

export interface RequestReportData {
  reportType: string;
  format: string;
  filters?: string;
  forceRefresh?: boolean;
}

export interface ReportStatusCounts {
  pending: number;
  processing: number;
  completed: number;
  failed: number;
}
