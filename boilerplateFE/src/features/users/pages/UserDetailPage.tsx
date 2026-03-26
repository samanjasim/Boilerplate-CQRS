import { useState, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { UserPlus, Pencil, X, Mail, Phone, Clock, ShieldCheck, Unlock, UserCheck, Ban, UserX } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { Button } from '@/components/ui/button';
import { PageHeader, InfoField, ConfirmDialog } from '@/components/common';
import { useUser, useActivateUser, useSuspendUser, useDeactivateUser, useUnlockUser } from '../api';
import { useRoles, useRemoveUserRole } from '@/features/roles/api';
import { RoleAssignModal, EditUserModal } from '../components';
import { usePermissions, useBackNavigation } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';
import { formatDate, formatDateTime } from '@/utils/format';
import { useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Pending: 'secondary',
  Suspended: 'destructive',
  Deactivated: 'destructive',
  Locked: 'destructive',
};

export default function UserDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const { data: user, isLoading } = useUser(id!);
  const { data: rolesData } = useRoles();
  const { mutate: removeRole, isPending: isRemovingRole } = useRemoveUserRole();
  const { hasPermission } = usePermissions();

  const [showAssignModal, setShowAssignModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [roleToRemove, setRoleToRemove] = useState<{ roleId: string; roleName: string } | null>(null);
  const [statusAction, setStatusAction] = useState<'suspend' | 'deactivate' | null>(null);

  const { mutate: activateUser } = useActivateUser();
  const { mutate: suspendUser, isPending: isSuspending } = useSuspendUser();
  const { mutate: deactivateUser, isPending: isDeactivating } = useDeactivateUser();
  const { mutate: unlockUser } = useUnlockUser();

  const canManageRoles = hasPermission(PERMISSIONS.Users.ManageRoles);
  const canUpdate = hasPermission(PERMISSIONS.Users.Update);
  useBackNavigation(ROUTES.USERS.LIST, t('users.backToUsers'));

  const allRoles = rolesData?.data ?? [];

  const roleIdMap = useMemo(
    () => new Map(allRoles.map((r) => [r.name, r.id])),
    [allRoles]
  );

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!user) {
    return <div className="text-muted-foreground">{t('common.noResults')}</div>;
  }

  const handleRemoveRole = () => {
    if (!roleToRemove) return;
    removeRole(
      { roleId: roleToRemove.roleId, userId: user.id },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: queryKeys.users.detail(user.id) });
          setRoleToRemove(null);
        },
      }
    );
  };

  const handleRoleAssigned = () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.users.detail(user.id) });
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${user.firstName} ${user.lastName}`}
        subtitle={`@${user.username}`}
      />

      {/* User Info */}
      <Card>
        <CardContent className="py-6">
          <div className="flex items-start gap-4 mb-6">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-lg font-bold text-primary">
              {user.firstName.charAt(0)}{user.lastName.charAt(0)}
            </div>
            <div className="min-w-0 flex-1">
              <h2 className="text-xl font-bold text-foreground">
                {user.firstName} {user.lastName}
              </h2>
              <p className="text-muted-foreground">@{user.username}</p>
            </div>
            <Badge variant={STATUS_VARIANT[user.status || 'Active'] || 'default'}>
              {user.status || t('common.active')}
            </Badge>
          </div>

          <div className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
            <InfoField label={t('users.userEmail')}>
              <div className="flex items-center gap-2">
                <Mail className="h-4 w-4 text-muted-foreground" />
                <span>{user.email}</span>
                {user.emailConfirmed && (
                  <Badge variant="secondary">{t('common.verified')}</Badge>
                )}
              </div>
            </InfoField>
            {user.phoneNumber && (
              <InfoField label={t('common.phone')}>
                <div className="flex items-center gap-2">
                  <Phone className="h-4 w-4 text-muted-foreground" />
                  <span>{user.phoneNumber}</span>
                  {user.phoneConfirmed && (
                    <Badge variant="secondary">{t('common.verified')}</Badge>
                  )}
                </div>
              </InfoField>
            )}
            <InfoField label={t('users.userCreated')}>
              {user.createdAt ? formatDate(user.createdAt, 'long') : '-'}
            </InfoField>
            {user.lastLoginAt && (
              <InfoField label={t('users.lastLogin')}>
                <div className="flex items-center gap-2">
                  <Clock className="h-4 w-4 text-muted-foreground" />
                  <span>{formatDateTime(user.lastLoginAt)}</span>
                </div>
              </InfoField>
            )}
          </div>

          {canUpdate && (
            <div className="flex items-center gap-2 border-t pt-4 mt-6">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowEditModal(true)}
              >
                <Pencil className="h-4 w-4" />
                {t('users.editUser')}
              </Button>
              {user.status === 'Locked' && (
                <Button variant="outline" size="sm" onClick={() => unlockUser(user.id)}>
                  <Unlock className="h-4 w-4" />
                  {t('users.unlock')}
                </Button>
              )}
              {(user.status === 'Suspended' || user.status === 'Deactivated') && (
                <Button variant="outline" size="sm" onClick={() => activateUser(user.id)}>
                  <UserCheck className="h-4 w-4" />
                  {t('users.activate')}
                </Button>
              )}
              {user.status === 'Active' && (
                <>
                  <Button variant="outline" size="sm" onClick={() => setStatusAction('suspend')}>
                    <Ban className="h-4 w-4" />
                    {t('users.suspend')}
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => setStatusAction('deactivate')}>
                    <UserX className="h-4 w-4" />
                    {t('users.deactivate')}
                  </Button>
                </>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Roles Management */}
      <Card>
        <CardContent className="py-6">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-lg font-semibold text-foreground">
              <ShieldCheck className="h-5 w-5 inline ltr:mr-2 rtl:ml-2 text-primary" />
              {t('users.userRoles')}
            </h3>
            {canManageRoles && (
              <Button
                size="sm"
                onClick={() => setShowAssignModal(true)}
              >
                <UserPlus className="h-4 w-4" />
                {t('users.assignRole')}
              </Button>
            )}
          </div>

          {!user.roles || user.roles.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">{t('users.noRolesAssigned')}</p>
          ) : (
            <div className="space-y-2">
              {user.roles.map((roleName) => {
                const roleId = roleIdMap.get(roleName);
                return (
                  <div
                    key={roleName}
                    className="flex items-center justify-between rounded-lg border px-4 py-3"
                  >
                    <div className="flex items-center gap-3">
                      <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10">
                        <ShieldCheck className="h-4 w-4 text-primary" />
                      </div>
                      <span className="text-sm font-medium text-foreground">{roleName}</span>
                    </div>
                    {canManageRoles && roleId && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setRoleToRemove({ roleId, roleName })}
                        className="text-muted-foreground hover:text-destructive"
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>

      {/* User Permissions (derived from roles) */}
      {user.permissions && user.permissions.length > 0 && (
        <Card>
          <CardContent className="py-6">
            <h3 className="mb-4 text-lg font-semibold text-foreground">
              {t('users.effectivePermissions')}
            </h3>
            <p className="mb-3 text-xs text-muted-foreground">
              {t('users.effectivePermissionsDesc')}
            </p>
            <div className="flex flex-wrap gap-2">
              {[...user.permissions].sort().map((perm) => (
                <Badge key={perm} variant="secondary">
                  {perm}
                </Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Assign Role Modal */}
      <RoleAssignModal
        isOpen={showAssignModal}
        onClose={() => setShowAssignModal(false)}
        userId={user.id}
        currentRoles={user.roles ?? []}
        onSuccess={handleRoleAssigned}
      />

      {/* Remove Role Confirmation */}
      <ConfirmDialog
        isOpen={!!roleToRemove}
        onClose={() => setRoleToRemove(null)}
        onConfirm={handleRemoveRole}
        title={t('users.removeRole')}
        description={t('users.removeRoleConfirm', { roleName: roleToRemove?.roleName, userName: `${user.firstName} ${user.lastName}` })}
        confirmLabel={t('users.removeRole')}
        isLoading={isRemovingRole}
      />

      {/* Edit User Modal */}
      {showEditModal && (
        <EditUserModal
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          user={user}
        />
      )}

      {/* Suspend / Deactivate Confirmation */}
      <ConfirmDialog
        isOpen={!!statusAction}
        onClose={() => setStatusAction(null)}
        onConfirm={() => {
          if (statusAction === 'suspend') {
            suspendUser(user.id, { onSuccess: () => setStatusAction(null) });
          } else if (statusAction === 'deactivate') {
            deactivateUser(user.id, { onSuccess: () => setStatusAction(null) });
          }
        }}
        title={statusAction === 'suspend' ? t('users.suspend') : t('users.deactivate')}
        description={
          statusAction === 'suspend'
            ? t('users.suspendConfirm', { name: `${user.firstName} ${user.lastName}` })
            : t('users.deactivateConfirm', { name: `${user.firstName} ${user.lastName}` })
        }
        confirmLabel={statusAction === 'suspend' ? t('users.suspend') : t('users.deactivate')}
        isLoading={isSuspending || isDeactivating}
      />
    </div>
  );
}
