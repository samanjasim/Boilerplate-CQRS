export const queryKeys = {
  auth: {
    all: ['auth'] as const,
    me: () => [...queryKeys.auth.all, 'me'] as const,
    sessions: () => [...queryKeys.auth.all, 'sessions'] as const,
    loginHistory: () => [...queryKeys.auth.all, 'login-history'] as const,
  },

  users: {
    all: ['users'] as const,
    lists: () => [...queryKeys.users.all, 'list'] as const,
    list: <T extends object>(filters?: T) => [...queryKeys.users.lists(), filters] as const,
    details: () => [...queryKeys.users.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.users.details(), id] as const,
  },

  roles: {
    all: ['roles'] as const,
    lists: () => [...queryKeys.roles.all, 'list'] as const,
    list: <T extends object>(filters?: T) => [...queryKeys.roles.lists(), filters] as const,
    details: () => [...queryKeys.roles.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.roles.details(), id] as const,
  },

  assignableRoles: {
    all: ['assignableRoles'] as const,
    list: (tenantId?: string) => [...['assignableRoles'], 'list', tenantId ?? 'none'] as const,
  },

  permissions: {
    all: ['permissions'] as const,
    list: () => [...queryKeys.permissions.all, 'list'] as const,
  },

  tenants: {
    all: ['tenants'] as const,
    lists: () => [...queryKeys.tenants.all, 'list'] as const,
    detail: (id: string) => [...queryKeys.tenants.all, 'detail', id] as const,
    branding: (slug?: string) => [...queryKeys.tenants.all, 'branding', slug ?? 'default'] as const,
  },

  invitations: {
    all: ['invitations'] as const,
    lists: () => [...queryKeys.invitations.all, 'list'] as const,
    list: <T extends object>(filters?: T) => [...queryKeys.invitations.lists(), filters] as const,
  },

  auditLogs: {
    all: () => ['auditLogs'] as const,
    list: () => [...queryKeys.auditLogs.all(), 'list'] as const,
  },

  notifications: {
    all: ['notifications'] as const,
    lists: () => [...queryKeys.notifications.all, 'list'] as const,
    list: <T extends object>(filters?: T) => [...queryKeys.notifications.lists(), filters] as const,
    unreadCount: () => [...queryKeys.notifications.all, 'unread-count'] as const,
    preferences: () => [...queryKeys.notifications.all, 'preferences'] as const,
  },

  files: {
    all: ['files'] as const,
    lists: () => [...queryKeys.files.all, 'list'] as const,
    list: (filters?: object) => [...queryKeys.files.lists(), filters ?? {}] as const,
    detail: (id: string) => [...queryKeys.files.all, 'detail', id] as const,
    url: (id: string) => [...queryKeys.files.all, 'url', id] as const,
  },

  reports: {
    all: ['reports'] as const,
    lists: () => [...queryKeys.reports.all, 'list'] as const,
    list: <T extends object>(filters?: T) => [...queryKeys.reports.lists(), filters] as const,
  },

  settings: {
    all: ['settings'] as const,
    list: () => [...queryKeys.settings.all, 'list'] as const,
  },

  apiKeys: {
    all: ['apiKeys'] as const,
    lists: () => [...queryKeys.apiKeys.all, 'list'] as const,
    list: (filters?: object) => [...queryKeys.apiKeys.lists(), filters ?? {}] as const,
    details: () => [...queryKeys.apiKeys.all, 'detail'] as const,
    detail: (id: string) => [...queryKeys.apiKeys.details(), id] as const,
  },

  featureFlags: {
    all: ['featureFlags'] as const,
    lists: () => [...queryKeys.featureFlags.all, 'list'] as const,
    list: (filters?: object) => [...queryKeys.featureFlags.lists(), filters ?? {}] as const,
    details: () => [...queryKeys.featureFlags.all, 'detail'] as const,
    detail: (key: string) => [...queryKeys.featureFlags.details(), key] as const,
  },

  billing: {
    all: ['billing'] as const,
    plans: {
      all: ['billing', 'plans'] as const,
      list: (params?: Record<string, unknown>) => ['billing', 'plans', 'list', params] as const,
      detail: (id: string) => ['billing', 'plans', 'detail', id] as const,
    },
    subscription: {
      all: ['billing', 'subscription'] as const,
      current: () => ['billing', 'subscription', 'current'] as const,
      tenant: (tenantId: string) => ['billing', 'subscription', 'tenant', tenantId] as const,
    },
    subscriptions: {
      all: ['billing', 'subscriptions'] as const,
      list: (params?: Record<string, unknown>) => ['billing', 'subscriptions', 'list', params] as const,
    },
    usage: {
      all: ['billing', 'usage'] as const,
      current: () => ['billing', 'usage', 'current'] as const,
      tenant: (tenantId: string) => ['billing', 'usage', 'tenant', tenantId] as const,
    },
    payments: {
      all: ['billing', 'payments'] as const,
      list: (params?: Record<string, unknown>) => ['billing', 'payments', 'list', params] as const,
      tenant: (tenantId: string, params?: Record<string, unknown>) => ['billing', 'payments', 'tenant', tenantId, params] as const,
    },
  },
} as const;
