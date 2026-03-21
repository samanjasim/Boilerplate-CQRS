import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { User, UserListParams, UpdateUserData, PaginatedResponse } from '@/types';

export const usersApi = {
  getUsers: async (params?: UserListParams): Promise<PaginatedResponse<User>> => {
    const response = await apiClient.get<PaginatedResponse<User>>(
      API_ENDPOINTS.USERS.LIST,
      { params },
    );
    return response.data;
  },

  getUserById: async (id: string): Promise<User> => {
    const response = await apiClient.get<{ data: User }>(API_ENDPOINTS.USERS.DETAIL(id));
    return response.data.data;
  },

  updateUser: async (id: string, data: UpdateUserData): Promise<void> => {
    await apiClient.put(API_ENDPOINTS.USERS.DETAIL(id), data);
  },

  activateUser: (id: string) => apiClient.post(API_ENDPOINTS.USERS.ACTIVATE(id)).then(r => r.data),

  suspendUser: (id: string) => apiClient.post(API_ENDPOINTS.USERS.SUSPEND(id)).then(r => r.data),

  deactivateUser: (id: string) => apiClient.post(API_ENDPOINTS.USERS.DEACTIVATE(id)).then(r => r.data),

  unlockUser: (id: string) => apiClient.post(API_ENDPOINTS.USERS.UNLOCK(id)).then(r => r.data),
};
