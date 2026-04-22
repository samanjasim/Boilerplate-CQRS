import { useTranslation } from 'react-i18next';
import { Lock, Building2, Globe2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { ResourceVisibility } from '@/features/access/types';

export function VisibilityBadge({ visibility }: { visibility: ResourceVisibility | null | undefined }) {
  const { t } = useTranslation();
  const map = {
    Private:    { icon: Lock,      variant: 'outline'    as const, key: 'access.visibility.private' },
    TenantWide: { icon: Building2, variant: 'secondary'  as const, key: 'access.visibility.tenantWide' },
    Public:     { icon: Globe2,    variant: 'default'    as const, key: 'access.visibility.public' },
  };
  const { icon: Icon, variant, key } = map[visibility ?? 'Private'] ?? map['Private'];
  return (
    <Badge variant={variant} className="gap-1">
      <Icon className="h-3 w-3" />
      {t(key)}
    </Badge>
  );
}
