import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  ReportRequest,
  ReportStatusCounts,
  RequestReportData,
  ApiResponse,
  PaginatedResponse,
} from '@/types';

export const reportsApi = {
  requestReport: async (data: RequestReportData): Promise<ReportRequest> => {
    const response = await apiClient.post<ApiResponse<ReportRequest>>(
      API_ENDPOINTS.REPORTS.REQUEST,
      data
    );
    return response.data.data;
  },

  getReports: async (
    params?: Record<string, unknown>
  ): Promise<PaginatedResponse<ReportRequest>> => {
    const response = await apiClient.get<PaginatedResponse<ReportRequest>>(
      API_ENDPOINTS.REPORTS.LIST,
      { params }
    );
    return response.data;
  },

  getStatusCounts: async (): Promise<ReportStatusCounts> => {
    const response = await apiClient.get<ApiResponse<ReportStatusCounts>>(
      API_ENDPOINTS.REPORTS.STATUS_COUNTS
    );
    return response.data.data;
  },

  getDownloadUrl: async (id: string): Promise<string> => {
    const response = await apiClient.get<ApiResponse<string>>(
      API_ENDPOINTS.REPORTS.DOWNLOAD(id)
    );
    return response.data.data;
  },

  deleteReport: async (id: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.REPORTS.DELETE(id));
  },
};
