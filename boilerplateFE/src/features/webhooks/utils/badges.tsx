import { Badge } from '@/components/ui/badge';
import type { WebhookDelivery } from '@/types';

export function statusBadge(status: WebhookDelivery['status']) {
  switch (status) {
    case 'Success':
      return (
        <Badge className="bg-success/10 text-success border-0 font-medium">
          {status}
        </Badge>
      );
    case 'Failed':
      return <Badge variant="destructive">{status}</Badge>;
    case 'Pending':
      return (
        <Badge className="bg-warning/10 text-warning border-0 font-medium">
          {status}
        </Badge>
      );
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}

export function lastStatusBadge(status: string | null) {
  if (!status) return <span className="text-muted-foreground">—</span>;
  switch (status) {
    case 'Success':
      return (
        <Badge className="bg-success/10 text-success border-0 font-medium text-xs">
          {status}
        </Badge>
      );
    case 'Failed':
      return <Badge variant="destructive" className="text-xs">{status}</Badge>;
    case 'Pending':
      return (
        <Badge className="bg-warning/10 text-warning border-0 font-medium text-xs">
          {status}
        </Badge>
      );
    default:
      return <Badge variant="secondary" className="text-xs">{status}</Badge>;
  }
}
