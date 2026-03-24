import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  SettingGroup,
  UpdateSettingData,
  ApiResponse,
} from '@/types';

export const settingsApi = {
  getSettings: async (): Promise<SettingGroup[]> => {
    const response = await apiClient.get<ApiResponse<SettingGroup[]>>(
      API_ENDPOINTS.SETTINGS.LIST
    );
    return response.data.data;
  },

  updateSettings: async (settings: UpdateSettingData[]): Promise<void> => {
    await apiClient.put(API_ENDPOINTS.SETTINGS.UPDATE, { settings });
  },

  updateSetting: async (key: string, value: string): Promise<void> => {
    await apiClient.put(API_ENDPOINTS.SETTINGS.UPDATE_SINGLE(key), { value });
  },
};
