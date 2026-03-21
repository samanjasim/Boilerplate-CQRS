import { Users, Shield, TrendingUp, Blocks } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { useAuthStore, selectUser } from '@/stores';
import { useUsers } from '@/features/users/api';
import { useRoles } from '@/features/roles/api';
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

export default function DashboardPage() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const { hasPermission } = usePermissions();

  const canViewUsers = hasPermission(PERMISSIONS.Users.View);
  const canViewRoles = hasPermission(PERMISSIONS.Roles.View);
  const canViewAuditLogs = hasPermission(PERMISSIONS.System.ViewAuditLogs);

  const { data: usersData } = useUsers({ enabled: canViewUsers });
  const { data: rolesData } = useRoles({ enabled: canViewRoles });

  const users = usersData?.data ?? [];
  const roles = rolesData?.data ?? [];
  const activeRoles = roles.filter((role) => role.isActive);

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
