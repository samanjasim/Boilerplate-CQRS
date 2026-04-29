import { useQuery } from '@tanstack/react-query';
import { API_ENDPOINTS } from '@/config/api.config';
import { apiClient } from '@/lib/axios';
import { queryKeys } from '@/lib/query/keys';
import type { ApiResponse } from '@/types/api.types';

export function WorkflowPendingTaskBadge() {
  const { data = 0 } = useQuery({
    queryKey: queryKeys.workflow.tasks.count(),
    queryFn: () =>
      apiClient
        .get<ApiResponse<number>>(API_ENDPOINTS.WORKFLOW.TASKS_COUNT)
        .then((r) => r.data.data),
  });

  if (data <= 0) return null;
  return (
    <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-primary-foreground font-mono">
      {data > 99 ? '99+' : data}
    </span>
  );
}
