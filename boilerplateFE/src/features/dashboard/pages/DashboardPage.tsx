import { Users, Shield, TrendingUp, Blocks, UserPlus, Pencil, Trash2, Activity } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { formatDistanceToNow } from 'date-fns';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useAuthStore, selectUser } from '@/stores';
import { useUsers, useSearchUsers } from '@/features/users/api';
import { useRoles } from '@/features/roles/api';
import { useAuditLogs } from '@/features/audit-logs/api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';

function StatCard({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: string | number;
  color: 'primary' | 'accent' | 'success' | 'info';
}) {
  const colors = {
    primary: 'bg-primary/10 text-primary',
    accent: 'bg-accent-500/10 text-accent-600 dark:bg-accent-500/20 dark:text-accent-400',
    success: 'bg-green-100 text-green-600 dark:bg-green-500/20 dark:text-green-400',
    info: 'bg-blue-100 text-blue-600 dark:bg-blue-500/20 dark:text-blue-400',
  };

  return (
    <Card>
      <CardContent className="py-6">
        <div className="flex items-center gap-4">
          <div className={`flex h-12 w-12 items-center justify-center rounded-xl ${colors[color]}`}>
            <Icon className="h-6 w-6" />
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            <p className="text-2xl font-bold text-foreground">{value}</p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

const ACTION_ICONS: Record<string, React.ElementType> = {
  Created: UserPlus,
  Updated: Pencil,
  Deleted: Trash2,
};

function getActionIcon(action: string) {
  return ACTION_ICONS[action] || Activity;
}

export default function DashboardPage() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const { hasPermission } = usePermissions();

  const canViewUsers = hasPermission(PERMISSIONS.Users.View);
  const canViewRoles = hasPermission(PERMISSIONS.Roles.View);
  const canViewAuditLogs = hasPermission(PERMISSIONS.System.ViewAuditLogs);

  const { data: usersData } = useUsers({ enabled: canViewUsers });
  const { data: rolesData } = useRoles({ enabled: canViewRoles });
  const { data: auditLogsData } = useAuditLogs(
    { pageSize: 5, pageNumber: 1, sortBy: 'performedAt', sortDescending: true },
    { enabled: canViewAuditLogs },
  );
  const { data: recentUsersData } = useSearchUsers(
    { pageSize: 5, pageNumber: 1, sortBy: 'createdAt', sortDescending: true },
    { enabled: canViewUsers },
  );

  const users = usersData?.data ?? [];
  const roles = rolesData?.data ?? [];
  const activeRoles = roles.filter((role) => role.isActive);
  const auditLogs = auditLogsData?.data ?? [];
  const recentUsers = recentUsersData?.data ?? [];

  return (
    <div className="space-y-8">
      {/* Welcome section */}
      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-primary/80 via-primary to-primary/90 p-8">
        <div className="relative z-10">
          <div className="flex items-center gap-3 mb-4">
            <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-white/20 shadow-lg">
              <Blocks className="h-7 w-7 text-white" />
            </div>
            <div>
              <h1 className="text-3xl font-bold text-white">
                {t('dashboard.welcomeBack', { name: user?.firstName })}
              </h1>
              <p className="text-white/80">
                {t('dashboard.subtitle')}
              </p>
            </div>
          </div>
        </div>
        <div className="absolute -right-20 -top-20 h-64 w-64 rounded-full bg-white/10 blur-3xl" />
        <div className="absolute -bottom-20 -left-20 h-64 w-64 rounded-full bg-white/5 blur-3xl" />
      </div>

      {/* Stats grid */}
      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard icon={Users} label={t('dashboard.totalUsers')} value={users.length} color="primary" />
        <StatCard icon={Shield} label={t('dashboard.activeRoles')} value={activeRoles.length} color="accent" />
        <StatCard icon={TrendingUp} label={t('dashboard.totalRoles')} value={roles.length} color="success" />
        <StatCard icon={Blocks} label={t('dashboard.platformStatus')} value={t('common.active')} color="info" />
      </div>

      {/* Recent Activity + Recent Users */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Recent Activity Feed */}
        {canViewAuditLogs && (
          <Card>
            <CardContent className="py-6">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-lg font-semibold text-foreground">{t('dashboard.recentActivity')}</h2>
                <Button variant="ghost" size="sm" asChild>
                  <Link to={ROUTES.AUDIT_LOGS.LIST}>{t('dashboard.viewAll')}</Link>
                </Button>
              </div>
              {auditLogs.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-8">
                  {t('dashboard.noRecentActivity')}
                </p>
              ) : (
                <div className="space-y-3">
                  {auditLogs.map((log) => {
                    const ActionIcon = getActionIcon(log.action);
                    return (
                      <div
                        key={log.id}
                        className="flex items-start gap-3 rounded-lg border px-4 py-3"
                      >
                        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                          <ActionIcon className="h-4 w-4 text-primary" />
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-2 flex-wrap">
                            <Badge variant="secondary">{log.entityType}</Badge>
                            <Badge variant="outline">{log.action}</Badge>
                          </div>
                          <p className="text-xs text-muted-foreground mt-1">
                            {log.performedByName || log.performedBy || '-'}
                          </p>
                        </div>
                        <span className="text-xs text-muted-foreground whitespace-nowrap">
                          {formatDistanceToNow(new Date(log.performedAt), { addSuffix: true })}
                        </span>
                      </div>
                    );
                  })}
                </div>
              )}
            </CardContent>
          </Card>
        )}

        {/* Recent Users Card */}
        {canViewUsers && (
          <Card>
            <CardContent className="py-6">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-lg font-semibold text-foreground">{t('dashboard.recentUsers')}</h2>
                <Button variant="ghost" size="sm" asChild>
                  <Link to={ROUTES.USERS.LIST}>{t('dashboard.viewAll')}</Link>
                </Button>
              </div>
              {recentUsers.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-8">
                  {t('dashboard.noRecentUsers')}
                </p>
              ) : (
                <div className="space-y-3">
                  {recentUsers.map((u) => (
                    <Link
                      key={u.id}
                      to={ROUTES.USERS.getDetail(u.id)}
                      className="flex items-center gap-3 rounded-lg border px-4 py-3 transition-colors hover:bg-muted/50"
                    >
                      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                        {u.firstName.charAt(0)}{u.lastName.charAt(0)}
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-foreground truncate">
                          {u.firstName} {u.lastName}
                        </p>
                        <p className="text-xs text-muted-foreground truncate">{u.email}</p>
                      </div>
                      <div className="flex flex-col items-end gap-1">
                        {u.roles && u.roles.length > 0 && (
                          <div className="flex gap-1">
                            {u.roles.slice(0, 2).map((role) => (
                              <Badge key={role} variant="secondary" className="text-xs">
                                {role}
                              </Badge>
                            ))}
                          </div>
                        )}
                        {u.createdAt && (
                          <span className="text-xs text-muted-foreground">
                            {formatDistanceToNow(new Date(u.createdAt), { addSuffix: true })}
                          </span>
                        )}
                      </div>
                    </Link>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        )}
      </div>

      {/* Quick overview */}
      <Card>
        <CardContent className="py-6">
          <h2 className="mb-4 text-lg font-semibold text-foreground">{t('dashboard.quickOverview')}</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {canViewUsers ? (
              <Link to={ROUTES.USERS.LIST} className="rounded-lg border p-4 transition-colors hover:bg-muted/50">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.usersManagement')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.usersManagementDesc')}</p>
              </Link>
            ) : (
              <div className="rounded-lg border p-4">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.usersManagement')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.usersManagementDesc')}</p>
              </div>
            )}
            {canViewRoles ? (
              <Link to={ROUTES.ROLES.LIST} className="rounded-lg border p-4 transition-colors hover:bg-muted/50">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.rolesPermissions')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.rolesPermissionsDesc')}</p>
              </Link>
            ) : (
              <div className="rounded-lg border p-4">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.rolesPermissions')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.rolesPermissionsDesc')}</p>
              </div>
            )}
            {canViewAuditLogs ? (
              <Link to={ROUTES.AUDIT_LOGS.LIST} className="rounded-lg border p-4 transition-colors hover:bg-muted/50">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.systemSettings')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.systemSettingsDesc')}</p>
              </Link>
            ) : (
              <div className="rounded-lg border p-4">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.systemSettings')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.systemSettingsDesc')}</p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
