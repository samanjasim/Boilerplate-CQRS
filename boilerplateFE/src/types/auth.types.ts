import type { User } from './user.types';

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface LoginResponse {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  refreshTokenExpiresAt: string | null;
  user: User | null;
  requiresTwoFactor: boolean;
}

export interface LoginCredentials {
  email: string;
  password: string;
  twoFactorCode?: string;
}

export interface Setup2FAResponse {
  secret: string;
  qrCodeUri: string;
}

export interface Verify2FAResponse {
  backupCodes: string[];
}

export interface Disable2FAData {
  code: string;
}

export interface RegisterData {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  firstName: string;
  lastName: string;
}

export interface ChangePasswordData {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

export interface RefreshTokenData {
  accessToken: string;
  refreshToken: string;
}

export interface Session {
  id: string;
  ipAddress: string | null;
  deviceInfo: string | null;
  createdAt: string;
  lastActiveAt: string;
  isCurrent: boolean;
}

export interface LoginHistoryEntry {
  id: string;
  email: string;
  ipAddress: string | null;
  deviceInfo: string | null;
  success: boolean;
  failureReason: string | null;
  createdAt: string;
}
