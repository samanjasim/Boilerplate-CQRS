import { useMemo, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Lock, Pencil, Shield, Trash2, Users } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { ConfirmDialog, PageHeader } from '@/components/common';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';
import { usePermissions } from '@/hooks';
import { formatDate } from '@/utils/format';
import { useRole, useDeleteRole } from '../api';

export default function RoleDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data: role, isLoading } = useRole(id!);
  const { mutate: deleteRole, isPending: isDeleting } = useDeleteRole();
  const { hasPermission } = usePermissions();
  const [showDeleteModal, setShowDeleteModal] = useState(false);

  const canUpdate = hasPermission(PERMISSIONS.Roles.Update);
  const canDelete = hasPermission(PERMISSIONS.Roles.Delete);
  const canManagePermissions = hasPermission(PERMISSIONS.Roles.ManagePermissions);

  const permissionsByModule = useMemo(
    () =>
      (role?.permissions ?? []).reduce<
        Record<string, NonNullable<typeof role>['permissions']>
      >((acc, perm) => {
        const module = perm.module || 'Other';
        (acc[module] ??= []).push(perm);
        return acc;
      }, {}),
    [role?.permissions],
  );

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!role) {
    return <div className="text-muted-foreground">{t('common.noResults')}</div>;
  }

  const handleDelete = () => {
    deleteRole(role.id, { onSuccess: () => navigate(ROUTES.ROLES.LIST) });
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={role.name}
        breadcrumbs={[
          { to: ROUTES.ROLES.LIST, label: t('roles.title') },
          { label: role.name },
        ]}
        actions={
          !role.isSystemRole ? (
            <div className="flex items-center gap-2">
              {(canUpdate || canManagePermissions) && (
                <Link to={ROUTES.ROLES.getEdit(role.id)}>
                  <Button variant="outline">
                    <Pencil className="h-4 w-4" />
                    {t('common.edit')}
                  </Button>
                </Link>
              )}
              {canDelete && (
                <Button variant="destructive" onClick={() => setShowDeleteModal(true)}>
                  <Trash2 className="h-4 w-4" />
                  {t('common.delete')}
                </Button>
              )}
            </div>
          ) : undefined
        }
      />

      {/* ─── Role hero card ─── */}
      <Card variant="glass" className="border border-border/40 overflow-hidden relative">
        <div
          aria-hidden
          className="pointer-events-none absolute -top-16 -right-12 h-48 w-48 rounded-full"
          style={{
            background:
              'radial-gradient(circle, color-mix(in srgb, var(--color-primary) 14%, transparent) 0%, transparent 70%)',
            filter: 'blur(20px)',
          }}
        />
        <CardContent className="py-6 px-6 relative">
          <div className="flex items-start gap-5">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl btn-primary-gradient glow-primary-sm">
              <Shield className="h-7 w-7 text-white" strokeWidth={2} />
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex flex-wrap items-center gap-2 mb-1">
                <h2 className="text-[22px] font-semibold tracking-tight font-display text-foreground">
                  {role.name}
                </h2>
                {role.isSystemRole && (
                  <Badge variant="outline" className="font-mono text-[10px]">
                    <Lock className="h-2.5 w-2.5 mr-1" />
                    {t('roles.systemRole')}
                  </Badge>
                )}
                <Badge variant={role.isActive ? 'healthy' : 'secondary'}>
                  {role.isActive ? t('common.active') : t('common.inactive')}
                </Badge>
              </div>
              {role.description && (
                <p className="text-[13px] text-muted-foreground leading-[1.5]">
                  {role.description}
                </p>
              )}
              <div className="flex flex-wrap gap-x-5 gap-y-1 mt-3 text-[12px] text-muted-foreground">
                <span className="flex items-center gap-1.5">
                  <Users className="h-3.5 w-3.5" />
                  {role.userCount} {t('roles.roleUsers').toLowerCase()}
                </span>
                <span className="flex items-center gap-1.5">
                  <Shield className="h-3.5 w-3.5" />
                  {role.permissions?.length ?? 0} {t('roles.rolePermissions').toLowerCase()}
                </span>
                {role.createdAt && (
                  <span>{t('users.userCreated')} {formatDate(role.createdAt)}</span>
                )}
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* ─── Permission matrix ─── */}
      {Object.keys(permissionsByModule).length > 0 && (
        <Card variant="glass" className="border border-border/40">
          <CardContent className="py-6 px-6">
            <h3 className="text-[15px] font-semibold text-foreground tracking-tight mb-5 flex items-center gap-2">
              <Shield className="h-4 w-4 text-primary" />
              {t('roles.rolePermissions')}
            </h3>
            <div className="space-y-5">
              {Object.entries(permissionsByModule)
                .sort(([a], [b]) => a.localeCompare(b))
                .map(([module, perms]) => (
                  <div key={module}>
                    <div className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted-foreground mb-2.5 pb-1.5 border-b border-border/30">
                      {module}
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      {perms!.map((perm) => (
                        <Badge key={perm.id} variant="secondary" className="font-mono text-[11px]">
                          {perm.name}
                        </Badge>
                      ))}
                    </div>
                  </div>
                ))}
            </div>
          </CardContent>
        </Card>
      )}

      <ConfirmDialog
        isOpen={showDeleteModal}
        onClose={() => setShowDeleteModal(false)}
        onConfirm={handleDelete}
        title={t('roles.deleteRole')}
        description={t('roles.deleteRoleConfirm', { name: role.name })}
        confirmLabel={t('roles.deleteRole')}
        isLoading={isDeleting}
      />
    </div>
  );
}
