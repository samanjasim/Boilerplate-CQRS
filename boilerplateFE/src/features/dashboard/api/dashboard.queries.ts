import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import type { DashboardAnalytics } from '@/types/dashboard.types';
import { dashboardApi } from './dashboard.api';

interface AnalyticsResponse {
  data: DashboardAnalytics;
}

export function useDashboardAnalytics(period: string = '30d') {
  return useQuery<AnalyticsResponse>({
    queryKey: queryKeys.dashboard.analytics(period),
    queryFn: () => dashboardApi.getAnalytics(period),
    staleTime: 5 * 60 * 1000, // 5 minutes — aligns with 15-min backend cache TTL
  });
}
