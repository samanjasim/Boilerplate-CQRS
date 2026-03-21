export const API_CONFIG = {
  BASE_URL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api/v1',
  TIMEOUT: 30000,
} as const;

export const API_ENDPOINTS = {
  AUTH: {
    LOGIN: '/Auth/login',
    REGISTER: '/Auth/register',
    REFRESH_TOKEN: '/Auth/refresh-token',
    ME: '/Auth/me',
    CHANGE_PASSWORD: '/Auth/change-password',
    SEND_EMAIL_VERIFICATION: '/Auth/send-email-verification',
    VERIFY_EMAIL: '/Auth/verify-email',
    FORGOT_PASSWORD: '/Auth/forgot-password',
    RESET_PASSWORD: '/Auth/reset-password',
  },
  USERS: {
    LIST: '/Users',
    DETAIL: (id: string) => `/Users/${id}`,
    ACTIVATE: (id: string) => `/Users/${id}/activate`,
    SUSPEND: (id: string) => `/Users/${id}/suspend`,
    DEACTIVATE: (id: string) => `/Users/${id}/deactivate`,
    UNLOCK: (id: string) => `/Users/${id}/unlock`,
  },
  ROLES: {
    LIST: '/Roles',
    DETAIL: (id: string) => `/Roles/${id}`,
    PERMISSIONS: (id: string) => `/Roles/${id}/permissions`,
    ASSIGN_USER: (roleId: string, userId: string) => `/Roles/${roleId}/users/${userId}`,
    REMOVE_USER: (roleId: string, userId: string) => `/Roles/${roleId}/users/${userId}`,
  },
  PERMISSIONS: {
    LIST: '/Permissions',
  },
  AUDIT_LOGS: {
    LIST: '/AuditLogs',
  },
} as const;
