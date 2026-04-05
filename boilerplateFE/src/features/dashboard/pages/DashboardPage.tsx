import { useState } from 'react';
import { Users, Shield, TrendingUp, Blocks, UserPlus, Pencil, Trash2, Activity } from 'lucide-react';
import { UserAvatar } from '@/components/common';
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
import { AnalyticsSummaryCards } from '../components/AnalyticsSummaryCards';
import { AnalyticsCharts } from '../components/AnalyticsCharts';
import { PeriodSelector } from '../components/PeriodSelector';
import { useDashboardAnalytics } from '../api';

const ACTION_ICONS: Record<string, React.ElementType> = {
  Created: UserPlus,
  Updated: Pencil,
  Deleted: Trash2,
};

function getActionIcon(action: string) {
  return ACTION_ICONS[action] || Activity;
}

function StatCardSkeletons() {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="animate-pulse bg-muted rounded-2xl h-32" />
      ))}
    </div>
  );
}

function ChartSkeletons() {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      {Array.from({ length: 2 }).map((_, i) => (
        <div key={i} className="animate-pulse bg-muted rounded-2xl h-64" />
      ))}
    </div>
  );
}

export default function DashboardPage() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const { hasPermission } = usePermissions();

  const [period, setPeriod] = useState('30d');

  const isTenantUser = !!user?.tenantId;

  const canViewUsers = hasPermission(PERMISSIONS.Users.View);
  const canViewRoles = hasPermission(PERMISSIONS.Roles.View);
  const canViewAuditLogs = hasPermission(PERMISSIONS.System.ViewAuditLogs);

  const { data: analyticsResponse, isLoading: analyticsLoading, isError: analyticsError } = useDashboardAnalytics(period);
  const analytics = analyticsResponse?.data;

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

  // Fallback stat cards used when analytics is not available
  const showFallbackStats = !analytics && !analyticsLoading;

  return (
    <div className="space-y-8">
      {/* Welcome section + Period selector */}
      <div className="flex items-center justify-between rounded-2xl gradient-hero px-8 py-7">
        <div>
          <h1 className="text-2xl font-semibold text-white tracking-tight">
            {t('dashboard.welcomeBack', { name: user?.firstName })}
          </h1>
          <p className="text-sm text-white mt-1">
            {t('dashboard.subtitle')}
          </p>
        </div>
        <div className="flex items-center gap-4">
          <PeriodSelector value={period} onChange={setPeriod} />
          <Blocks className="h-12 w-12 text-white/30" />
        </div>
      </div>

      {/* Analytics error banner */}
      {analyticsError && (
        <p className="text-sm text-muted-foreground text-center">
          {t('dashboard.analyticsUnavailable', 'Analytics data is temporarily unavailable.')}
        </p>
      )}

      {/* Stat cards — analytics-powered or skeleton while loading */}
      {analytics ? (
        <AnalyticsSummaryCards analytics={analytics} period={period} />
      ) : analyticsLoading ? (
        <StatCardSkeletons />
      ) : showFallbackStats ? (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="hover-lift">
            <CardContent className="py-6">
              <div className="flex items-center gap-4">
                <div className="flex h-11 w-11 items-center justify-center rounded-xl [background:var(--active-bg)] [color:var(--active-text)]">
                  <Users className="h-6 w-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{isTenantUser ? t('dashboard.myUsers') : t('dashboard.totalUsers')}</p>
                  <p className="text-2xl font-bold text-foreground">{users.length}</p>
                </div>
              </div>
            </CardContent>
          </Card>
          <Card className="hover-lift">
            <CardContent className="py-6">
              <div className="flex items-center gap-4">
                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-accent-500/10 text-accent-600">
                  <Shield className="h-6 w-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('dashboard.activeRoles')}</p>
                  <p className="text-2xl font-bold text-foreground">{activeRoles.length}</p>
                </div>
              </div>
            </CardContent>
          </Card>
          <Card className="hover-lift">
            <CardContent className="py-6">
              <div className="flex items-center gap-4">
                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-green-500/10 text-green-600">
                  <TrendingUp className="h-6 w-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('dashboard.totalRoles')}</p>
                  <p className="text-2xl font-bold text-foreground">{roles.length}</p>
                </div>
              </div>
            </CardContent>
          </Card>
          <Card className="hover-lift">
            <CardContent className="py-6">
              <div className="flex items-center gap-4">
                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-500/10 text-blue-600">
                  <Blocks className="h-6 w-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{isTenantUser ? t('dashboard.myOrganization') : t('dashboard.platformStatus')}</p>
                  <p className="text-2xl font-bold text-foreground">{t('common.active')}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      ) : null}

      {/* Analytics charts */}
      {analytics ? (
        <AnalyticsCharts analytics={analytics} />
      ) : analyticsLoading ? (
        <ChartSkeletons />
      ) : null}

      {/* Recent Activity + Recent Users */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Recent Activity Feed */}
        {canViewAuditLogs && (
          <Card>
            <CardContent className="py-6">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-base font-semibold text-foreground tracking-tight">{t('dashboard.recentActivity')}</h2>
                <Button variant="link" size="sm" asChild>
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
                        className="flex items-start gap-3 rounded-xl px-4 py-3 transition-colors duration-150 hover:bg-secondary"
                      >
                        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg [background:var(--active-bg)]">
                          <ActionIcon className="h-4 w-4 [color:var(--active-text)]" />
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
                <h2 className="text-base font-semibold text-foreground tracking-tight">{t('dashboard.recentUsers')}</h2>
                <Button variant="link" size="sm" asChild>
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
                      className="flex items-center gap-3 rounded-xl px-4 py-3 transition-colors duration-150 hover:bg-secondary"
                    >
                      <UserAvatar firstName={u.firstName} lastName={u.lastName} size="sm" />
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
              <Link to={ROUTES.USERS.LIST} className="rounded-2xl bg-card p-5 shadow-card transition-all duration-200 hover:shadow-card-hover">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.usersManagement')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.usersManagementDesc')}</p>
              </Link>
            ) : (
              <div className="rounded-2xl bg-card p-5 shadow-card">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.usersManagement')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.usersManagementDesc')}</p>
              </div>
            )}
            {canViewRoles ? (
              <Link to={ROUTES.ROLES.LIST} className="rounded-2xl bg-card p-5 shadow-card transition-all duration-200 hover:shadow-card-hover">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.rolesPermissions')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.rolesPermissionsDesc')}</p>
              </Link>
            ) : (
              <div className="rounded-2xl bg-card p-5 shadow-card">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.rolesPermissions')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.rolesPermissionsDesc')}</p>
              </div>
            )}
            {canViewAuditLogs ? (
              <Link to={ROUTES.AUDIT_LOGS.LIST} className="rounded-2xl bg-card p-5 shadow-card transition-all duration-200 hover:shadow-card-hover">
                <h3 className="text-sm font-medium text-foreground">{t('dashboard.systemSettings')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.systemSettingsDesc')}</p>
              </Link>
            ) : (
              <div className="rounded-2xl bg-card p-5 shadow-card">
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
