import { useQuery } from '@tanstack/react-query';
import { auditLogsApi } from './audit-logs.api';
import { queryKeys } from '@/lib/query/keys';

export function useAuditLogs(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: [...queryKeys.auditLogs.list(), params],
    queryFn: () => auditLogsApi.getAuditLogs(params),
  });
}
