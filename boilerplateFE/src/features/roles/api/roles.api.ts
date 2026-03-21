import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { Role, Permission, CreateRoleData, UpdateRoleData, UpdateRolePermissionsData, ApiResponse, PaginatedResponse } from '@/types';

interface PermissionGroup {
  module: string;
  permissions: Permission[];
}

export const rolesApi = {
  getRoles: async (): Promise<PaginatedResponse<Role>> => {
    const response = await apiClient.get<PaginatedResponse<Role>>(API_ENDPOINTS.ROLES.LIST);
    return response.data;
  },

  getRoleById: async (id: string): Promise<Role> => {
    const response = await apiClient.get<ApiResponse<Role>>(API_ENDPOINTS.ROLES.DETAIL(id));
    return response.data.data;
  },

  createRole: async (data: CreateRoleData): Promise<string> => {
    const response = await apiClient.post<ApiResponse<string>>(API_ENDPOINTS.ROLES.LIST, data);
    return response.data.data;
  },

  updateRole: async (id: string, data: UpdateRoleData): Promise<Role> => {
    const response = await apiClient.put<ApiResponse<Role>>(API_ENDPOINTS.ROLES.DETAIL(id), data);
    return response.data.data;
  },

  deleteRole: async (id: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.ROLES.DETAIL(id));
  },

  updateRolePermissions: async (id: string, data: UpdateRolePermissionsData): Promise<void> => {
    await apiClient.put(API_ENDPOINTS.ROLES.PERMISSIONS(id), data);
  },

  getPermissions: async (): Promise<Permission[]> => {
    const response = await apiClient.get<ApiResponse<PermissionGroup[]>>(API_ENDPOINTS.PERMISSIONS.LIST);
    return response.data.data.flatMap((group) => group.permissions);
  },

  assignUserToRole: async (roleId: string, userId: string): Promise<void> => {
    await apiClient.post(API_ENDPOINTS.ROLES.ASSIGN_USER(roleId, userId));
  },

  removeUserFromRole: async (roleId: string, userId: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.ROLES.REMOVE_USER(roleId, userId));
  },
};
