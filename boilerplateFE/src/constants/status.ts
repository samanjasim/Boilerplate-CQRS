/** Badge variant mapping for entity status values (Active, Pending, Suspended, etc.) */
export const STATUS_BADGE_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Pending: 'secondary',
  Suspended: 'destructive',
  Deactivated: 'destructive',
  Locked: 'destructive',

  // Subscription statuses
  Trialing: 'secondary',
  PastDue: 'destructive',
  Canceled: 'destructive',
  Expired: 'outline',

  // Payment statuses
  Completed: 'default',
  Failed: 'destructive',
  Refunded: 'outline',

  // Communication statuses
  Inactive: 'secondary',
  Error: 'destructive',
  Queued: 'secondary',
  Sending: 'secondary',
  Delivered: 'default',
  Bounced: 'destructive',
};
