import { useQuery } from '@tanstack/react-query';
import { auditLogsApi } from './audit-logs.api';
import { queryKeys } from '@/lib/query/keys';

export function useAuditLogs(params?: Record<string, unknown>, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: [...queryKeys.auditLogs.list(), params],
    queryFn: () => auditLogsApi.getAuditLogs(params),
    ...options,
  });
}

export function useAuditLog(id: string | undefined, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.auditLogs.detail(id ?? ''),
    queryFn: () => auditLogsApi.getAuditLog(id!),
    enabled: !!id && (options?.enabled ?? true),
    staleTime: 60_000,
  });
}
