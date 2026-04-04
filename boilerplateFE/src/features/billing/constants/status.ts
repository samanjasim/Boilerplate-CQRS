export const PAYMENT_STATUS_VARIANT: Record<string | number, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  0: 'secondary', 1: 'default', 2: 'destructive', 3: 'outline',
  Pending: 'secondary', Completed: 'default', Failed: 'destructive', Refunded: 'outline',
};

export const PAYMENT_STATUS_LABEL: Record<string | number, string> = {
  0: 'Pending', 1: 'Completed', 2: 'Failed', 3: 'Refunded',
  Pending: 'Pending', Completed: 'Completed', Failed: 'Failed', Refunded: 'Refunded',
};

export const SUBSCRIPTION_STATUS: Record<string | number, string> = {
  0: 'Trialing', 1: 'Active', 2: 'PastDue', 3: 'Canceled', 4: 'Expired',
  Trialing: 'Trialing', Active: 'Active', PastDue: 'PastDue', Canceled: 'Canceled', Expired: 'Expired',
};

export const BILLING_INTERVAL: Record<string | number, string> = {
  0: 'Monthly', 1: 'Annual', Monthly: 'Monthly', Annual: 'Annual',
};
