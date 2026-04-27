import { useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Ban,
  Calendar,
  Clock,
  Mail,
  Pencil,
  Phone,
  ShieldCheck,
  Unlock,
  UserCheck,
  UserPlus,
  UserX,
  X,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { ConfirmDialog, InfoField, PageHeader, UserAvatar } from '@/components/common';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import { ROUTES } from '@/config';
import { usePermissions } from '@/hooks';
import { useRoles, useRemoveUserRole } from '@/features/roles/api';
import { queryKeys } from '@/lib/query/keys';
import { formatDate, formatDateTime } from '@/utils/format';
import { useQueryClient } from '@tanstack/react-query';
import { EditUserModal, RoleAssignModal } from '../components';
import {
  useUser,
  useActivateUser,
  useSuspendUser,
  useDeactivateUser,
  useUnlockUser,
} from '../api';

type Tab = 'profile' | 'roles';

export default function UserDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const { data: user, isLoading } = useUser(id!);
  const { data: rolesData } = useRoles();
  const { mutate: removeRole, isPending: isRemovingRole } = useRemoveUserRole();
  const { hasPermission } = usePermissions();

  const [activeTab, setActiveTab] = useState<Tab>('profile');
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

  const allRoles = useMemo(() => rolesData?.data ?? [], [rolesData?.data]);
  const roleIdMap = useMemo(
    () => new Map(allRoles.map((r) => [r.name, r.id])),
    [allRoles],
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
      },
    );
  };

  const handleRoleAssigned = () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.users.detail(user.id) });
  };

  /* ── Status badge ── */
  const statusVariant = STATUS_BADGE_VARIANT[user.status || 'Active'] ?? 'default';
  const statusLabel = user.status || t('common.active');

  const tabs = [
    { id: 'profile' as Tab, label: t('profile.title') },
    { id: 'roles' as Tab, label: t('users.userRoles') },
  ];

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${user.firstName} ${user.lastName}`}
        subtitle={`@${user.username}`}
        breadcrumbs={[
          { to: ROUTES.USERS.LIST, label: t('users.title') },
          { label: `${user.firstName} ${user.lastName}` },
        ]}
        tabs={tabs.map((tab) => ({
          label: tab.label,
          active: activeTab === tab.id,
          onClick: () => setActiveTab(tab.id),
        }))}
      />

      {/* ─── Hero header card ─── */}
      <Card variant="glass" className="border border-border/40 overflow-hidden relative">
        {/* Subtle corner glow */}
        <div
          aria-hidden
          className="pointer-events-none absolute -top-20 -right-16 h-60 w-60 rounded-full"
          style={{
            background:
              'radial-gradient(circle, color-mix(in srgb, var(--color-primary) 14%, transparent) 0%, transparent 70%)',
            filter: 'blur(24px)',
          }}
        />
        <CardContent className="py-7 px-7 relative">
          <div className="flex flex-col sm:flex-row sm:items-start gap-5">
            <UserAvatar firstName={user.firstName} lastName={user.lastName} size="xl" />

            <div className="flex-1 min-w-0">
              <div className="flex flex-wrap items-center gap-2 mb-1">
                <h2 className="text-[22px] font-semibold tracking-tight font-display text-foreground">
                  {user.firstName} {user.lastName}
                </h2>
                <Badge variant={statusVariant as any}>{statusLabel}</Badge>
              </div>

              <p className="text-[13px] text-muted-foreground mb-3">@{user.username}</p>

              {/* Meta row */}
              <div className="flex flex-wrap gap-x-5 gap-y-1.5 text-[12px] text-muted-foreground">
                <span className="flex items-center gap-1.5">
                  <Mail className="h-3.5 w-3.5" />
                  {user.email}
                  {user.emailConfirmed && (
                    <Badge variant="secondary" className="text-[10px] py-0 px-1.5">
                      {t('common.verified')}
                    </Badge>
                  )}
                </span>
                {user.phoneNumber && (
                  <span className="flex items-center gap-1.5">
                    <Phone className="h-3.5 w-3.5" />
                    {user.phoneNumber}
                  </span>
                )}
                {user.createdAt && (
                  <span className="flex items-center gap-1.5">
                    <Calendar className="h-3.5 w-3.5" />
                    {t('users.userCreated')} {formatDate(user.createdAt)}
                  </span>
                )}
                {user.lastLoginAt && (
                  <span className="flex items-center gap-1.5">
                    <Clock className="h-3.5 w-3.5" />
                    {t('users.lastLogin')} {formatDateTime(user.lastLoginAt)}
                  </span>
                )}
              </div>

              {/* Role pills */}
              {user.roles && user.roles.length > 0 && (
                <div className="flex flex-wrap gap-1.5 mt-3">
                  {user.roles.map((role) => (
                    <Badge key={role} variant="outline" className="text-[11px]">
                      <ShieldCheck className="h-3 w-3 mr-1 text-primary" />
                      {role}
                    </Badge>
                  ))}
                </div>
              )}
            </div>

            {/* Action buttons */}
            {canUpdate && (
              <div className="flex flex-wrap sm:flex-col gap-2 sm:items-end shrink-0">
                <Button variant="outline" size="sm" onClick={() => setShowEditModal(true)}>
                  <Pencil className="h-3.5 w-3.5" />
                  {t('users.editUser')}
                </Button>
                {user.status === 'Locked' && (
                  <Button variant="outline" size="sm" onClick={() => unlockUser(user.id)}>
                    <Unlock className="h-3.5 w-3.5" />
                    {t('users.unlock')}
                  </Button>
                )}
                {(user.status === 'Suspended' || user.status === 'Deactivated') && (
                  <Button variant="outline" size="sm" onClick={() => activateUser(user.id)}>
                    <UserCheck className="h-3.5 w-3.5" />
                    {t('users.activate')}
                  </Button>
                )}
                {user.status === 'Active' && (
                  <>
                    <Button variant="outline" size="sm" onClick={() => setStatusAction('suspend')}>
                      <Ban className="h-3.5 w-3.5" />
                      {t('users.suspend')}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setStatusAction('deactivate')}
                    >
                      <UserX className="h-3.5 w-3.5" />
                      {t('users.deactivate')}
                    </Button>
                  </>
                )}
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* ─── Tab content ─── */}
      {activeTab === 'profile' && (
        <Card variant="glass" className="border border-border/40">
          <CardContent className="py-6 px-6">
            <div className="grid gap-x-6 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
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
                {user.createdAt ? formatDate(user.createdAt, 'long') : '—'}
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
          </CardContent>
        </Card>
      )}

      {activeTab === 'roles' && (
        <div className="space-y-4">
          {/* Roles management */}
          <Card variant="glass" className="border border-border/40">
            <CardContent className="py-6 px-6">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-[15px] font-semibold text-foreground tracking-tight flex items-center gap-2">
                  <ShieldCheck className="h-4 w-4 text-primary" />
                  {t('users.userRoles')}
                </h3>
                {canManageRoles && (
                  <Button size="sm" onClick={() => setShowAssignModal(true)}>
                    <UserPlus className="h-3.5 w-3.5" />
                    {t('users.assignRole')}
                  </Button>
                )}
              </div>

              {!user.roles || user.roles.length === 0 ? (
                <p className="text-sm text-muted-foreground py-4 text-center">
                  {t('users.noRolesAssigned')}
                </p>
              ) : (
                <div className="space-y-2">
                  {user.roles.map((roleName) => {
                    const roleId = roleIdMap.get(roleName);
                    return (
                      <div
                        key={roleName}
                        className="flex items-center justify-between rounded-xl border border-border/40 bg-secondary/30 px-4 py-3"
                      >
                        <div className="flex items-center gap-3">
                          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10">
                            <ShieldCheck className="h-4 w-4 text-primary" />
                          </div>
                          <span className="text-[13px] font-medium text-foreground">{roleName}</span>
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

          {/* Effective permissions */}
          {user.permissions && user.permissions.length > 0 && (
            <Card variant="glass" className="border border-border/40">
              <CardContent className="py-6 px-6">
                <h3 className="text-[15px] font-semibold text-foreground tracking-tight mb-1.5">
                  {t('users.effectivePermissions')}
                </h3>
                <p className="text-[12px] text-muted-foreground mb-4">
                  {t('users.effectivePermissionsDesc')}
                </p>
                <div className="flex flex-wrap gap-1.5">
                  {[...user.permissions].sort().map((perm) => (
                    <Badge key={perm} variant="secondary" className="font-mono text-[11px]">
                      {perm}
                    </Badge>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      )}

      {/* ─── Modals ─── */}
      <RoleAssignModal
        isOpen={showAssignModal}
        onClose={() => setShowAssignModal(false)}
        userId={user.id}
        currentRoles={user.roles ?? []}
        onSuccess={handleRoleAssigned}
      />

      <ConfirmDialog
        isOpen={!!roleToRemove}
        onClose={() => setRoleToRemove(null)}
        onConfirm={handleRemoveRole}
        title={t('users.removeRole')}
        description={t('users.removeRoleConfirm', {
          roleName: roleToRemove?.roleName,
          userName: `${user.firstName} ${user.lastName}`,
        })}
        confirmLabel={t('users.removeRole')}
        isLoading={isRemovingRole}
      />

      {showEditModal && (
        <EditUserModal
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          user={user}
        />
      )}

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
