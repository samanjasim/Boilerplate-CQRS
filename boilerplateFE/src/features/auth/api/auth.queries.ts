import { useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { authApi } from './auth.api';
import { queryKeys } from '@/lib/query/keys';
import { useAuthStore } from '@/stores';
import { storage } from '@/utils';
import { ROUTES } from '@/config';
import i18n from '@/i18n';
import type { LoginCredentials, RegisterData, ChangePasswordData } from '@/types';

export function useCurrentUser() {
  return useQuery({
    queryKey: queryKeys.auth.me(),
    queryFn: () => authApi.getMe(),
    enabled: !!storage.getAccessToken(),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { login, setLoading } = useAuthStore();

  return useMutation({
    mutationFn: (credentials: LoginCredentials) => authApi.login(credentials),
    onSuccess: async (loginResponse) => {
      storage.setTokens(loginResponse.accessToken, loginResponse.refreshToken);

      const tokens = {
        accessToken: loginResponse.accessToken,
        refreshToken: loginResponse.refreshToken,
      };

      // Fetch full user with permissions from /me
      const fullUser = await authApi.getMe(loginResponse.accessToken);

      login(fullUser, tokens);
      queryClient.setQueryData(queryKeys.auth.me(), fullUser);
      toast.success(i18n.t('auth.welcomeBackUser', { name: fullUser.firstName }));
      navigate(ROUTES.DASHBOARD);
    },
    onError: () => {
      setLoading(false);
    },
  });
}

export function useRegister() {
  const navigate = useNavigate();

  return useMutation({
    mutationFn: (data: RegisterData) => authApi.register(data),
    onSuccess: (_, variables) => {
      toast.success(i18n.t('auth.accountCreated'));
      navigate(ROUTES.VERIFY_EMAIL, { state: { email: variables.email } });
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { logout } = useAuthStore();

  return useCallback(() => {
    storage.clearTokens();
    logout();
    queryClient.clear();
    navigate(ROUTES.LOGIN);
  }, [queryClient, navigate, logout]);
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (data: ChangePasswordData) => authApi.changePassword(data),
    onSuccess: () => {
      toast.success(i18n.t('auth.passwordChanged'));
    },
  });
}

export function useSendEmailVerification() {
  return useMutation({
    mutationFn: (data: { email: string }) => authApi.sendEmailVerification(data),
    onSuccess: () => {
      toast.success(i18n.t('auth.verificationCodeSent'));
    },
  });
}

export function useVerifyEmail() {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (data: { email: string; code: string }) => authApi.verifyEmail(data),
    onSuccess: () => {
      toast.success(i18n.t('auth.emailVerified'));
      navigate(ROUTES.LOGIN);
    },
  });
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: (data: { email: string }) => authApi.forgotPassword(data),
    onSuccess: () => {
      toast.success(i18n.t('auth.resetCodeSent'));
    },
  });
}

export function useResetPassword() {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (data: {
      email: string;
      code: string;
      newPassword: string;
      confirmNewPassword: string;
    }) => authApi.resetPassword(data),
    onSuccess: () => {
      toast.success(i18n.t('auth.passwordResetSuccess'));
      navigate(ROUTES.LOGIN);
    },
  });
}
