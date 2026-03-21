export interface AuditLog {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  changes: string | null;
  performedBy: string | null;
  performedByName: string | null;
  performedAt: string;
  ipAddress: string | null;
  correlationId: string | null;
}
