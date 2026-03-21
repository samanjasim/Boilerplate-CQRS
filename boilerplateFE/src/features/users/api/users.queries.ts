import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import i18n from '@/i18n';
import { usersApi } from './users.api';
import { queryKeys } from '@/lib/query/keys';
import type { UserListParams, UpdateUserData } from '@/types';

export function useUsers(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.users.lists(),
    queryFn: () => usersApi.getUsers(),
    enabled: options?.enabled,
  });
}

export function useSearchUsers(params: UserListParams, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.users.list(params),
    queryFn: () => usersApi.getUsers(params),
    enabled: options?.enabled,
  });
}

export function useUser(id: string) {
  return useQuery({
    queryKey: queryKeys.users.detail(id),
    queryFn: () => usersApi.getUserById(id),
    enabled: !!id,
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateUserData }) =>
      usersApi.updateUser(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      toast.success(i18n.t('users.userUpdated'));
    },
  });
}

export function useActivateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => usersApi.activateUser(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.detail(id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      toast.success(i18n.t('users.userActivated'));
    },
  });
}

export function useSuspendUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => usersApi.suspendUser(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.detail(id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      toast.success(i18n.t('users.userSuspended'));
    },
  });
}

export function useDeactivateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => usersApi.deactivateUser(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.detail(id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      toast.success(i18n.t('users.userDeactivated'));
    },
  });
}

export function useUnlockUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => usersApi.unlockUser(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.users.detail(id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
      toast.success(i18n.t('users.userUnlocked'));
    },
  });
}
