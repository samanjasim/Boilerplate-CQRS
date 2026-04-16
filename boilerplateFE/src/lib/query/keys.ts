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
    resolves: () => [...queryKeys.featureFlags.all, 'resolve'] as const,
    resolve: (key: string) => [...queryKeys.featureFlags.resolves(), key] as const,
  },

  webhooks: {
    all: ['webhooks'] as const,
    endpoints: {
      all: ['webhooks', 'endpoints'] as const,
      list: () => ['webhooks', 'endpoints', 'list'] as const,
      detail: (id: string) => ['webhooks', 'endpoints', 'detail', id] as const,
    },
    deliveries: {
      list: (id: string, params?: Record<string, unknown>) => ['webhooks', 'deliveries', id, params] as const,
    },
    eventTypes: () => ['webhooks', 'event-types'] as const,
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
  products: {
    all: ['products'] as const,
    lists: () => ['products', 'list'] as const,
    list: (params?: Record<string, unknown>) => ['products', 'list', params] as const,
    details: () => ['products', 'detail'] as const,
    detail: (id: string) => ['products', 'detail', id] as const,
  },
  communication: {
    all: ['communication'] as const,
    channelConfigs: {
      all: ['communication', 'channel-configs'] as const,
      list: () => ['communication', 'channel-configs', 'list'] as const,
      detail: (id: string) => ['communication', 'channel-configs', 'detail', id] as const,
    },
    providers: () => ['communication', 'providers'] as const,
    templates: {
      all: ['communication', 'templates'] as const,
      list: (category?: string) => ['communication', 'templates', 'list', category] as const,
      detail: (id: string) => ['communication', 'templates', 'detail', id] as const,
      categories: () => ['communication', 'templates', 'categories'] as const,
    },
    triggerRules: {
      all: ['communication', 'trigger-rules'] as const,
      list: () => ['communication', 'trigger-rules', 'list'] as const,
      detail: (id: string) => ['communication', 'trigger-rules', 'detail', id] as const,
    },
    events: {
      list: () => ['communication', 'events', 'list'] as const,
    },
    integrationConfigs: {
      all: ['communication', 'integration-configs'] as const,
      list: () => ['communication', 'integration-configs', 'list'] as const,
      detail: (id: string) => ['communication', 'integration-configs', 'detail', id] as const,
    },
    preferences: {
      all: ['communication', 'preferences'] as const,
      list: () => ['communication', 'preferences', 'list'] as const,
    },
    required: {
      all: ['communication', 'required'] as const,
      list: () => ['communication', 'required', 'list'] as const,
    },
    deliveryLogs: {
      all: ['communication', 'delivery-logs'] as const,
      list: (params?: Record<string, unknown>) => ['communication', 'delivery-logs', 'list', params] as const,
      detail: (id: string) => ['communication', 'delivery-logs', 'detail', id] as const,
    },
    dashboard: () => ['communication', 'dashboard'] as const,
  },
  importExport: {
    all: ['importExport'] as const,
    types: () => ['importExport', 'types'] as const,
    imports: {
      all: ['importExport', 'imports'] as const,
      list: (params?: Record<string, unknown>) => ['importExport', 'imports', 'list', params] as const,
      detail: (id: string) => ['importExport', 'imports', 'detail', id] as const,
    },
  },
  commentsActivity: {
    all: ['commentsActivity'] as const,
    comments: {
      all: ['commentsActivity', 'comments'] as const,
      list: (entityType: string, entityId: string, params?: Record<string, unknown>) =>
        ['commentsActivity', 'comments', 'list', entityType, entityId, params] as const,
    },
    activity: {
      all: ['commentsActivity', 'activity'] as const,
      list: (entityType: string, entityId: string, params?: Record<string, unknown>) =>
        ['commentsActivity', 'activity', 'list', entityType, entityId, params] as const,
    },
    timeline: {
      all: ['commentsActivity', 'timeline'] as const,
      list: (entityType: string, entityId: string, params?: Record<string, unknown>) =>
        ['commentsActivity', 'timeline', 'list', entityType, entityId, params] as const,
    },
    watchers: {
      all: ['commentsActivity', 'watchers'] as const,
      status: (entityType: string, entityId: string) =>
        ['commentsActivity', 'watchers', 'status', entityType, entityId] as const,
    },
    mentionableUsers: (search?: string, entityType?: string, entityId?: string) =>
      ['commentsActivity', 'mentionable-users', search, entityType, entityId] as const,
  },
} as const;
