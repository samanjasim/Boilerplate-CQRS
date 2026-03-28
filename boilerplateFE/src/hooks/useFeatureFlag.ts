import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { featureFlagsApi } from '@/features/feature-flags/api';

export function useFeatureFlag(key: string) {
  const { data, isLoading } = useQuery({
    queryKey: queryKeys.featureFlags.detail(key),
    queryFn: () => featureFlagsApi.getByKey(key),
    enabled: !!key,
    staleTime: 5 * 60 * 1000,
  });

  return {
    value: data?.resolvedValue ?? null,
    isEnabled: data?.resolvedValue === 'true',
    isLoading,
    flag: data ?? null,
  };
}
