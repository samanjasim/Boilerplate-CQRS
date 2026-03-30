import { useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { authApi } from './auth.api';
import { queryKeys } from '@/lib/query/keys';
import { useAuthStore, useUIStore } from '@/stores';
import { storage, getTenantSlug, getTenantUrl, getMainDomainUrl } from '@/utils';
import { ROUTES } from '@/config';
import i18n from '@/i18n';
import type { LoginCredentials, RegisterData, RegisterTenantData, ChangePasswordData, Disable2FAData, PaginationParams } from '@/types';

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
      // Check if 2FA is required
      if (loginResponse.requiresTwoFactor) {
        // Don't navigate or set tokens - the LoginForm will handle showing the 2FA input
        return;
      }

      storage.setTokens(loginResponse.accessToken!, loginResponse.refreshToken!);

      const tokens = {
        accessToken: loginResponse.accessToken!,
        refreshToken: loginResponse.refreshToken!,
      };

      // Fetch full user with permissions from /me
      const fullUser = await authApi.getMe(loginResponse.accessToken!);

      login(fullUser, tokens);
      queryClient.setQueryData(queryKeys.auth.me(), fullUser);
      // Always set tenant — clears stale value for platform admins (null)
      useUIStore.getState().setActiveTenantId(fullUser.tenantId ?? null);
      toast.success(i18n.t('auth.welcomeBackUser', { name: fullUser.firstName }));

      // Check if user belongs to a tenant with a slug
      if (fullUser.tenantSlug) {
        window.location.href = getTenantUrl(fullUser.tenantSlug, '/dashboard');
        return;
      }
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

export function useRegisterTenant() {
  const navigate = useNavigate();

  return useMutation({
    mutationFn: (data: RegisterTenantData) => authApi.registerTenant(data),
    onSuccess: (_, variables) => {
      toast.success(i18n.t('auth.accountCreated'));
      navigate(ROUTES.VERIFY_EMAIL, { state: { email: variables.email } });
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  const { logout } = useAuthStore();

  return useCallback(() => {
    const slug = getTenantSlug();
    storage.clearTokens();
    logout();
    useUIStore.getState().setActiveTenantId(null);
    useUIStore.getState().setTenantSlug(null);
    queryClient.clear();
    if (slug) {
      window.location.href = `${getMainDomainUrl()}/login`;
    } else {
      window.location.href = '/login';
    }
  }, [queryClient, logout]);
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

export function useSetup2FA() {
  return useMutation({
    mutationFn: () => authApi.setup2FA(),
  });
}

export function useVerify2FA() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { secret: string; code: string }) => authApi.verify2FA(data),
    onSuccess: () => {
      toast.success(i18n.t('twoFactor.enabledSuccess'));
      queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() });
    },
  });
}

export function useDisable2FA() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Disable2FAData) => authApi.disable2FA(data),
    onSuccess: () => {
      toast.success(i18n.t('twoFactor.disabledSuccess'));
      queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() });
    },
  });
}

export function useInviteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { email: string; roleId?: string; tenantId?: string }) => authApi.inviteUser(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.invitations.all });
      toast.success(i18n.t('invitations.inviteSent'));
    },
  });
}

export function useAcceptInvite() {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (data: {
      token: string;
      firstName: string;
      lastName: string;
      password: string;
      confirmPassword: string;
    }) => authApi.acceptInvite(data),
    onSuccess: () => {
      toast.success(i18n.t('invitations.inviteAccepted'));
      navigate(ROUTES.LOGIN);
    },
  });
}

export function useInvitations(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.invitations.lists(),
    queryFn: () => authApi.getInvitations(),
    enabled: options?.enabled,
  });
}

export function useRevokeInvitation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => authApi.revokeInvitation(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.invitations.all });
      toast.success(i18n.t('invitations.inviteRevoked'));
    },
  });
}

export function useSessions() {
  return useQuery({
    queryKey: queryKeys.auth.sessions(),
    queryFn: () => authApi.getSessions(),
  });
}

export function useRevokeSession() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => authApi.revokeSession(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.auth.sessions() });
      toast.success(i18n.t('sessions.sessionRevoked'));
    },
  });
}

export function useRevokeAllSessions() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => authApi.revokeAllSessions(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.auth.sessions() });
      toast.success(i18n.t('sessions.allSessionsRevoked'));
    },
  });
}

export function useLoginHistory(params?: PaginationParams) {
  return useQuery({
    queryKey: [...queryKeys.auth.loginHistory(), params],
    queryFn: () => authApi.getLoginHistory(params),
  });
}
