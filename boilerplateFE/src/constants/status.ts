/** Badge variant mapping for entity status values (Active, Pending, Suspended, etc.) */
export const STATUS_BADGE_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Pending: 'secondary',
  Suspended: 'destructive',
  Deactivated: 'destructive',
  Locked: 'destructive',
};
