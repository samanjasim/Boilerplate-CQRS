import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import i18n from '@/i18n';
import { rolesApi } from './roles.api';
import { queryKeys } from '@/lib/query/keys';
import type { CreateRoleData, UpdateRoleData, UpdateRolePermissionsData } from '@/types';

export function useRoles(options?: { params?: Record<string, unknown>; enabled?: boolean }) {
  return useQuery({
    queryKey: options?.params ? queryKeys.roles.list(options.params) : queryKeys.roles.lists(),
    queryFn: () => rolesApi.getRoles(options?.params),
    enabled: options?.enabled,
  });
}

export function useRole(id: string) {
  return useQuery({
    queryKey: queryKeys.roles.detail(id),
    queryFn: () => rolesApi.getRoleById(id),
    enabled: !!id,
  });
}

export function useAllPermissions(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.permissions.list(),
    queryFn: () => rolesApi.getPermissions(),
    enabled: options?.enabled,
  });
}

export function useCreateRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateRoleData) => rolesApi.createRole(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.all });
      toast.success(i18n.t('roles.roleCreated'));
    },
  });
}

export function useUpdateRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateRoleData }) => rolesApi.updateRole(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.detail(variables.id) });
      toast.success(i18n.t('roles.roleUpdated'));
    },
  });
}

export function useDeleteRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => rolesApi.deleteRole(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.all });
      toast.success(i18n.t('roles.roleDeleted'));
    },
  });
}

export function useUpdateRolePermissions() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateRolePermissionsData }) =>
      rolesApi.updateRolePermissions(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.detail(variables.id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.all });
      toast.success(i18n.t('roles.permissionsUpdated'));
    },
  });
}

export function useAssignableRoles(tenantId?: string, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.assignableRoles.list(tenantId),
    queryFn: () => rolesApi.getAssignableRoles(tenantId),
    enabled: options?.enabled,
  });
}

export function useAssignUserRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ roleId, userId }: { roleId: string; userId: string }) =>
      rolesApi.assignUserToRole(roleId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.all });
      toast.success(i18n.t('roles.roleAssigned'));
    },
  });
}

export function useRemoveUserRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ roleId, userId }: { roleId: string; userId: string }) =>
      rolesApi.removeUserFromRole(roleId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.roles.all });
      toast.success(i18n.t('roles.roleRemoved'));
    },
  });
}
