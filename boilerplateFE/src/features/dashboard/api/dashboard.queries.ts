import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { dashboardApi } from './dashboard.api';

export function useDashboardAnalytics(period: string = '30d') {
  return useQuery({
    queryKey: queryKeys.dashboard.analytics(period),
    queryFn: () => dashboardApi.getAnalytics(period),
  });
}
