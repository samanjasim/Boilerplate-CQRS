import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { PaginatedResponse, AuditLog } from '@/types';

export const auditLogsApi = {
  getAuditLogs: async (
    params?: Record<string, unknown>
  ): Promise<PaginatedResponse<AuditLog>> => {
    const response = await apiClient.get<PaginatedResponse<AuditLog>>(
      API_ENDPOINTS.AUDIT_LOGS.LIST,
      { params }
    );
    return response.data;
  },

  getAuditLog: async (id: string): Promise<AuditLog> => {
    const response = await apiClient.get<{ data: AuditLog }>(
      API_ENDPOINTS.AUDIT_LOGS.BY_ID(id)
    );
    return response.data.data ?? (response.data as unknown as AuditLog);
  },
};
