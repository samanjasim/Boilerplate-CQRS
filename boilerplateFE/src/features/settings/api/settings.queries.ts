import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { settingsApi } from './settings.api';
import { queryKeys } from '@/lib/query/keys';
import i18n from '@/i18n';
import type { UpdateSettingData } from '@/types';

export function useSettings() {
  return useQuery({
    queryKey: queryKeys.settings.list(),
    queryFn: () => settingsApi.getSettings(),
  });
}

export function useUpdateSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (settings: UpdateSettingData[]) => settingsApi.updateSettings(settings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.settings.all });
      toast.success(i18n.t('settings.saved'));
    },
  });
}

export function useUpdateSetting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ key, value }: UpdateSettingData) => settingsApi.updateSetting(key, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.settings.all });
      toast.success(i18n.t('settings.saved'));
    },
  });
}
