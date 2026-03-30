import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import { storage } from '@/utils';
import type {
  LoginCredentials,
  RegisterData,
  RegisterTenantData,
  ChangePasswordData,
  User,
  AuthTokens,
  LoginResponse,
  ApiResponse,
  Setup2FAResponse,
  Verify2FAResponse,
  Disable2FAData,
  PaginatedResponse,
  Session,
  LoginHistoryEntry,
  PaginationParams,
} from '@/types';

export const authApi = {
  login: async (credentials: LoginCredentials): Promise<LoginResponse> => {
    const response = await apiClient.post<ApiResponse<LoginResponse>>(
      API_ENDPOINTS.AUTH.LOGIN,
      credentials
    );
    return response.data.data;
  },

  register: async (data: RegisterData): Promise<void> => {
    await apiClient.post(API_ENDPOINTS.AUTH.REGISTER, data);
  },

  refreshToken: async (
    refreshToken: string
  ): Promise<AuthTokens> => {
    const response = await apiClient.post<ApiResponse<AuthTokens>>(
      API_ENDPOINTS.AUTH.REFRESH_TOKEN,
      { refreshToken }
    );
    return response.data.data;
  },

  getMe: async (token?: string): Promise<User> => {
    const response = await apiClient.get<ApiResponse<User>>(API_ENDPOINTS.AUTH.ME, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    return response.data.data;
  },

  changePassword: async (data: ChangePasswordData): Promise<void> => {
    await apiClient.post(API_ENDPOINTS.AUTH.CHANGE_PASSWORD, data);
  },

  sendEmailVerification: (data: { email: string }) =>
    apiClient.post(API_ENDPOINTS.AUTH.SEND_EMAIL_VERIFICATION, data).then((r) => r.data),

  verifyEmail: (data: { email: string; code: string }) =>
    apiClient.post(API_ENDPOINTS.AUTH.VERIFY_EMAIL, data).then((r) => r.data),

  forgotPassword: (data: { email: string }) =>
    apiClient.post(API_ENDPOINTS.AUTH.FORGOT_PASSWORD, data).then((r) => r.data),

  resetPassword: (data: {
    email: string;
    code: string;
    newPassword: string;
    confirmNewPassword: string;
  }) => apiClient.post(API_ENDPOINTS.AUTH.RESET_PASSWORD, data).then((r) => r.data),

  registerTenant: (data: RegisterTenantData) =>
    apiClient.post(API_ENDPOINTS.AUTH.REGISTER_TENANT, data).then((r) => r.data),

  setup2FA: async (): Promise<Setup2FAResponse> => {
    const response = await apiClient.post<ApiResponse<Setup2FAResponse>>(
      API_ENDPOINTS.AUTH.SETUP_2FA
    );
    return response.data.data;
  },

  verify2FA: async (data: { secret: string; code: string }): Promise<Verify2FAResponse> => {
    const response = await apiClient.post<ApiResponse<Verify2FAResponse>>(
      API_ENDPOINTS.AUTH.VERIFY_2FA,
      data
    );
    return response.data.data;
  },

  disable2FA: async (data: Disable2FAData): Promise<void> => {
    await apiClient.post(API_ENDPOINTS.AUTH.DISABLE_2FA, data);
  },

  inviteUser: (data: { email: string; roleId?: string; tenantId?: string }) =>
    apiClient.post<ApiResponse<string>>(API_ENDPOINTS.AUTH.INVITE_USER, data).then((r) => r.data.data),

  acceptInvite: (data: {
    token: string;
    firstName: string;
    lastName: string;
    password: string;
    confirmPassword: string;
  }) => apiClient.post(API_ENDPOINTS.AUTH.ACCEPT_INVITE, data).then((r) => r.data),

  getInvitations: async (): Promise<PaginatedResponse<Invitation>> => {
    const response = await apiClient.get<PaginatedResponse<Invitation>>(
      API_ENDPOINTS.AUTH.INVITATIONS,
    );
    return response.data;
  },

  revokeInvitation: (id: string) =>
    apiClient.delete(API_ENDPOINTS.AUTH.REVOKE_INVITATION(id)).then((r) => r.data),

  getSessions: async (): Promise<Session[]> => {
    const refreshToken = storage.getRefreshToken();
    const response = await apiClient.get<ApiResponse<Session[]>>(
      API_ENDPOINTS.AUTH.SESSIONS,
      { headers: refreshToken ? { 'X-Refresh-Token': refreshToken } : undefined }
    );
    return response.data.data;
  },

  revokeSession: async (id: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.AUTH.SESSION(id));
  },

  revokeAllSessions: async (): Promise<void> => {
    const refreshToken = storage.getRefreshToken();
    await apiClient.delete(API_ENDPOINTS.AUTH.SESSIONS, {
      headers: refreshToken ? { 'X-Refresh-Token': refreshToken } : undefined,
    });
  },

  getLoginHistory: async (params?: PaginationParams): Promise<PaginatedResponse<LoginHistoryEntry>> => {
    const response = await apiClient.get<PaginatedResponse<LoginHistoryEntry>>(
      API_ENDPOINTS.AUTH.LOGIN_HISTORY,
      { params }
    );
    return response.data;
  },
};

export interface Invitation {
  id: string;
  email: string;
  roleName: string;
  invitedByName: string;
  expiresAt: string;
  isAccepted: boolean;
  createdAt: string;
}
