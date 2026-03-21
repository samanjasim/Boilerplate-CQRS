export const ROUTES = {
  // Auth
  LOGIN: '/login',
  REGISTER: '/register',
  VERIFY_EMAIL: '/verify-email',
  FORGOT_PASSWORD: '/forgot-password',
  RESET_PASSWORD: '/reset-password',

  // Dashboard
  DASHBOARD: '/',

  // Users
  USERS: {
    LIST: '/users',
    DETAIL: '/users/:id',
    getDetail: (id: string) => `/users/${id}`,
  },

  // Roles
  ROLES: {
    LIST: '/roles',
    CREATE: '/roles/new',
    DETAIL: '/roles/:id',
    EDIT: '/roles/:id/edit',
    getDetail: (id: string) => `/roles/${id}`,
    getEdit: (id: string) => `/roles/${id}/edit`,
  },
  // Audit Logs
  AUDIT_LOGS: {
    LIST: '/audit-logs',
  },
} as const;
