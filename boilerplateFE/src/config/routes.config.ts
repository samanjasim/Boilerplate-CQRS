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

  // Webhooks
  WEBHOOKS: '/webhooks',
  WEBHOOKS_ADMIN: {
    LIST: '/webhooks/admin',
    DETAIL: '/webhooks/admin/:endpointId',
    getDetail: (endpointId: string) => `/webhooks/admin/${endpointId}`,
  },

  // Feature Flags
  FEATURE_FLAGS: {
    LIST: '/feature-flags',
  },

  // Import / Export
  IMPORT_EXPORT: '/import-export',

  // Communication
  COMMUNICATION: {
    CHANNELS: '/communication/channels',
    TEMPLATES: '/communication/templates',
    TRIGGER_RULES: '/communication/trigger-rules',
    INTEGRATIONS: '/communication/integrations',
    DELIVERY_LOG: '/communication/delivery-log',
  },

  // Products
  PRODUCTS: {
    LIST: '/products',
    CREATE: '/products/new',
    DETAIL: '/products/:id',
    getDetail: (id: string) => `/products/${id}`,
  },

  // Workflows
  WORKFLOWS: {
    INBOX: '/workflows/inbox',
    INSTANCES: '/workflows/instances',
    INSTANCE_DETAIL: '/workflows/instances/:id',
    getInstanceDetail: (id: string) => `/workflows/instances/${id}`,
    DEFINITIONS: '/workflows/definitions',
    DEFINITION_DETAIL: '/workflows/definitions/:id',
    getDefinitionDetail: (id: string) => `/workflows/definitions/${id}`,
    DEFINITION_DESIGNER: '/workflows/definitions/:id/designer',
    getDefinitionDesigner: (id: string) => `/workflows/definitions/${id}/designer`,
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
