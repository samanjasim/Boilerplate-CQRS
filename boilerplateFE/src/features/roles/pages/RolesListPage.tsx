import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Plus, Shield } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState } from '@/components/common';
import { useRoles } from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';

export default function RolesListPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const { data, isLoading, isError } = useRoles();
  const roles = data?.data ?? [];

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('roles.title')} />
        <EmptyState icon={Shield} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('roles.title')}
        subtitle={t('roles.allRoles')}
        actions={
          hasPermission(PERMISSIONS.Roles.Create) ? (
            <Link to={ROUTES.ROLES.CREATE}>
              <Button>
                <Plus className="h-4 w-4" />
                {t('roles.createRole')}
              </Button>
            </Link>
          ) : undefined
        }
      />

      {roles.length === 0 ? (
        <EmptyState icon={Shield} title={t('common.noResults')} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {roles.map((role) => (
            <Link key={role.id} to={ROUTES.ROLES.getDetail(role.id)}>
              <Card className="hover:shadow-md transition-shadow cursor-pointer h-full">
                <CardContent className="py-4">
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                      <Shield className="h-5 w-5 text-primary" />
                    </div>
                    <Badge variant={role.isActive ? 'default' : 'secondary'}>
                      {role.isActive ? t('common.active') : t('common.inactive')}
                    </Badge>
                  </div>
                  <h3 className="font-semibold text-foreground">{role.name}</h3>
                  {role.description && (
                    <p className="mt-1 text-sm text-muted-foreground line-clamp-2">{role.description}</p>
                  )}
                  <div className="mt-3 flex items-center gap-4 text-xs text-muted-foreground">
                    <span>{role.userCount} {t('roles.roleUsers').toLowerCase()}</span>
                    <span>{role.permissions?.length || 0} {t('roles.rolePermissions').toLowerCase()}</span>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
