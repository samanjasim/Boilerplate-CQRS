export const ROUTES = {
  // Public
  LANDING: '/',
  REGISTER_TENANT: '/register-tenant',

  // Auth
  LOGIN: '/login',
  REGISTER: '/register',
  VERIFY_EMAIL: '/verify-email',
  FORGOT_PASSWORD: '/forgot-password',
  RESET_PASSWORD: '/reset-password',
  ACCEPT_INVITE: '/accept-invite',

  // Dashboard
  DASHBOARD: '/dashboard',

  // Profile
  PROFILE: '/profile',

  // Notifications
  NOTIFICATIONS: '/notifications',

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

  // Tenants
  TENANTS: {
    LIST: '/tenants',
    DETAIL: '/tenants/:id',
    getDetail: (id: string) => `/tenants/${id}`,
  },

  // Organization (tenant self-service)
  ORGANIZATION: '/organization',

  // Audit Logs
  AUDIT_LOGS: {
    LIST: '/audit-logs',
  },

  // Files
  FILES: {
    LIST: '/files',
  },

  // Reports
  REPORTS: {
    LIST: '/reports',
  },

  // Settings
  SETTINGS: '/settings',

  // API Keys
  API_KEYS: {
    LIST: '/api-keys',
  },

  // Feature Flags
  FEATURE_FLAGS: {
    LIST: '/feature-flags',
  },

  // Billing
  BILLING: '/billing',
  BILLING_PLANS: '/billing/plans',
  PRICING: '/pricing',

  // Subscriptions (platform admin)
  SUBSCRIPTIONS: {
    LIST: '/billing/subscriptions',
    DETAIL: '/billing/subscriptions/:tenantId',
    getDetail: (tenantId: string) => `/billing/subscriptions/${tenantId}`,
  },
} as const;
