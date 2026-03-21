import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  LoginCredentials,
  RegisterData,
  RegisterTenantData,
  ChangePasswordData,
  User,
  AuthTokens,
  LoginResponse,
  ApiResponse,
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
};
