import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { UserCheck, Ban, UserX } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { Button } from '@/components/ui/button';
import { PageHeader, InfoField, ConfirmDialog } from '@/components/common';
import { useTenant, useActivateTenant, useSuspendTenant, useDeactivateTenant } from '../api';
import { ROUTES } from '@/config';
import { format } from 'date-fns';

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Pending: 'secondary',
  Suspended: 'destructive',
  Deactivated: 'destructive',
};

export default function TenantDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const { data: tenant, isLoading } = useTenant(id!);

  const [statusAction, setStatusAction] = useState<'suspend' | 'deactivate' | null>(null);

  const { mutate: activateTenant } = useActivateTenant();
  const { mutate: suspendTenant, isPending: isSuspending } = useSuspendTenant();
  const { mutate: deactivateTenant, isPending: isDeactivating } = useDeactivateTenant();

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!tenant) {
    return <div className="text-muted-foreground">{t('common.noResults')}</div>;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={tenant.name}
        backTo={ROUTES.TENANTS.LIST}
        backLabel={t('tenants.backToTenants')}
      />

      <Card>
        <CardContent className="py-6">
          <div className="flex items-start gap-4 mb-6">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-lg font-bold text-primary">
              {tenant.name.charAt(0)}
            </div>
            <div className="min-w-0 flex-1">
              <h2 className="text-xl font-bold text-foreground">{tenant.name}</h2>
              {tenant.slug && (
                <p className="text-muted-foreground">{tenant.slug}</p>
              )}
            </div>
            <Badge variant={STATUS_VARIANT[tenant.status] || 'default'}>
              {tenant.status}
            </Badge>
          </div>

          <div className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
            <InfoField label={t('tenants.name')}>
              <span>{tenant.name}</span>
            </InfoField>
            <InfoField label={t('tenants.slug')}>
              <span>{tenant.slug || '-'}</span>
            </InfoField>
            <InfoField label={t('common.createdAt')}>
              {tenant.createdAt ? format(new Date(tenant.createdAt), 'MMMM d, yyyy') : '-'}
            </InfoField>
          </div>

          <div className="flex items-center gap-2 border-t pt-4 mt-6">
            {(tenant.status === 'Suspended' || tenant.status === 'Deactivated') && (
              <Button variant="outline" size="sm" onClick={() => activateTenant(id!)}>
                <UserCheck className="h-4 w-4" />
                {t('tenants.activate')}
              </Button>
            )}
            {tenant.status === 'Active' && (
              <>
                <Button variant="outline" size="sm" onClick={() => setStatusAction('suspend')}>
                  <Ban className="h-4 w-4" />
                  {t('tenants.suspend')}
                </Button>
                <Button variant="outline" size="sm" onClick={() => setStatusAction('deactivate')}>
                  <UserX className="h-4 w-4" />
                  {t('tenants.deactivate')}
                </Button>
              </>
            )}
          </div>
        </CardContent>
      </Card>

      <ConfirmDialog
        isOpen={!!statusAction}
        onClose={() => setStatusAction(null)}
        onConfirm={() => {
          if (statusAction === 'suspend') {
            suspendTenant(id!, { onSuccess: () => setStatusAction(null) });
          } else if (statusAction === 'deactivate') {
            deactivateTenant(id!, { onSuccess: () => setStatusAction(null) });
          }
        }}
        title={statusAction === 'suspend' ? t('tenants.suspend') : t('tenants.deactivate')}
        description={
          statusAction === 'suspend'
            ? t('tenants.suspendConfirm', { name: tenant.name })
            : t('tenants.deactivateConfirm', { name: tenant.name })
        }
        confirmLabel={statusAction === 'suspend' ? t('tenants.suspend') : t('tenants.deactivate')}
        isLoading={isSuspending || isDeactivating}
      />
    </div>
  );
}
