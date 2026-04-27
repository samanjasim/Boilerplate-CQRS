import { useEffect, useRef, useState } from 'react';
import {
  Activity,
  ArrowRight,
  Blocks,
  ClipboardList,
  Pencil,
  Shield,
  TrendingUp,
  Trash2,
  UserPlus,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import { useTimeAgoFormatter } from '@/hooks';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { UserAvatar } from '@/components/common';
import { Slot } from '@/lib/extensions';
import { cn } from '@/lib/utils';
import { useAuthStore, selectUser } from '@/stores';
import { useUsers, useSearchUsers } from '@/features/users/api';
import { useRoles } from '@/features/roles/api';
import { useAuditLogs } from '@/features/audit-logs/api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';

/* ───────────────────────────────────────────────────────────────────────────
 * Helpers
 * ─────────────────────────────────────────────────────────────────────── */

type StatTone = 'copper' | 'emerald' | 'violet' | 'amber';

const TONE_BG: Record<StatTone, string> = {
  copper: 'btn-primary-gradient glow-primary-sm',
  emerald:
    'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]',
  violet:
    'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]',
  amber:
    'bg-gradient-to-br from-[var(--color-amber-400)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_30%,transparent)]',
};

function useCountUp(target: number, duration = 1000) {
  const [value, setValue] = useState(0);
  const reduced = useRef(false);

  useEffect(() => {
    if (typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      reduced.current = true;
      setValue(target);
      return;
    }
    if (target === 0) {
      setValue(0);
      return;
    }
    const start = performance.now();
    const tick = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      const eased = 1 - Math.pow(1 - t, 3);
      setValue(Math.round(target * eased));
      if (t < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }, [target, duration]);

  return value;
}

const ACTION_ICONS: Record<string, LucideIcon> = {
  Created: UserPlus,
  Updated: Pencil,
  Deleted: Trash2,
};
const ACTION_TONE: Record<string, StatTone> = {
  Created: 'emerald',
  Updated: 'copper',
  Deleted: 'amber',
};

function actionIcon(a: string): LucideIcon {
  return ACTION_ICONS[a] ?? Activity;
}
function actionTone(a: string): StatTone {
  return ACTION_TONE[a] ?? 'violet';
}

/* ───────────────────────────────────────────────────────────────────────────
 * Stat card
 * ─────────────────────────────────────────────────────────────────────── */

interface StatCardProps {
  icon: LucideIcon;
  label: string;
  value: number | string;
  delta?: string;
  tone: StatTone;
  spark?: string;
}

function StatCard({ icon: Icon, label, value, delta, tone, spark }: StatCardProps) {
  const numericTarget = typeof value === 'number' ? value : 0;
  const animatedValue = useCountUp(numericTarget);
  const display = typeof value === 'number' ? animatedValue.toLocaleString() : value;

  return (
    <div className="surface-glass hover-lift-card rounded-2xl p-5 border border-border/40">
      <div className="flex items-start justify-between gap-3 mb-3">
        <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-white ${TONE_BG[tone]}`}>
          <Icon className="h-[18px] w-[18px]" strokeWidth={2} />
        </div>
        {spark && (
          <svg viewBox="0 0 100 30" className="h-7 w-20 shrink-0" preserveAspectRatio="none">
            <defs>
              <linearGradient id={`stat-spark-${label}`} x1="0" x2="1" y1="0" y2="0">
                <stop offset="0%" stopColor="var(--color-primary-700)" />
                <stop offset="100%" stopColor="var(--color-violet-500)" />
              </linearGradient>
            </defs>
            <path
              d={spark}
              fill="none"
              stroke={`url(#stat-spark-${label})`}
              strokeWidth="1.4"
              strokeLinecap="round"
              strokeLinejoin="round"
              opacity="0.45"
            />
            <path
              d={spark}
              pathLength={100}
              fill="none"
              stroke={`url(#stat-spark-${label})`}
              strokeWidth="1.8"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="spark-shimmer"
            />
          </svg>
        )}
      </div>
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted-foreground mb-1">
        {label}
      </div>
      <div className="text-[32px] font-light tracking-[-0.025em] leading-none font-display text-foreground mb-1.5 font-feature-settings">
        {display}
      </div>
      {delta && (
        <div className="text-[11px] font-mono text-[var(--color-accent-700)] dark:text-[var(--color-accent-300)] inline-flex items-center gap-1">
          <TrendingUp className="h-3 w-3" />
          {delta}
        </div>
      )}
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────────────────
 * Page
 * ─────────────────────────────────────────────────────────────────────── */

export default function DashboardPage() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const { hasPermission } = usePermissions();
  const formatTimeAgo = useTimeAgoFormatter();

  const isTenantUser = !!user?.tenantId;

  const canViewUsers = hasPermission(PERMISSIONS.Users.View);
  const canViewRoles = hasPermission(PERMISSIONS.Roles.View);
  const canViewAuditLogs = hasPermission(PERMISSIONS.System.ViewAuditLogs);

  const { data: usersData } = useUsers({ enabled: canViewUsers });
  const { data: rolesData } = useRoles({ enabled: canViewRoles });
  const { data: auditLogsData } = useAuditLogs(
    { pageSize: 6, pageNumber: 1, sortBy: 'performedAt', sortDescending: true },
    { enabled: canViewAuditLogs },
  );
  const { data: recentUsersData } = useSearchUsers(
    { pageSize: 5, pageNumber: 1, sortBy: 'createdAt', sortDescending: true },
    { enabled: canViewUsers },
  );

  const users = usersData?.data ?? [];
  const roles = rolesData?.data ?? [];
  const activeRoles = roles.filter((r) => r.isActive);
  const auditLogs = auditLogsData?.data ?? [];
  const recentUsers = recentUsersData?.data ?? [];

  const heroMetric = users.length;
  const heroAnimated = useCountUp(heroMetric, 1200);

  return (
    <div className="space-y-6">
      {/* ─── Hero greeting ─── */}
      <section className="surface-glass rounded-2xl p-7 lg:p-8 border border-border/40 shadow-card relative overflow-hidden">
        {/* Soft inner glow accent */}
        <div
          aria-hidden
          className="pointer-events-none absolute -top-32 -right-24 h-80 w-80 rounded-full"
          style={{
            background:
              'radial-gradient(circle, color-mix(in srgb, var(--color-primary) 18%, transparent) 0%, transparent 65%)',
            filter: 'blur(20px)',
          }}
        />
        <div className="relative flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
          <div>
            <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2 inline-flex items-center gap-2">
              <span className="pulse-dot" />
              {t('dashboard.live')} · {isTenantUser ? user?.tenantName ?? t('dashboard.workspace') : t('dashboard.platform')}
            </div>
            <h1 className="text-[32px] sm:text-[40px] font-extralight tracking-[-0.03em] leading-[1.1] font-display text-foreground">
              {t('dashboard.welcomeBack', { name: user?.firstName })}
            </h1>
            <p className="text-sm text-muted-foreground mt-2 max-w-md leading-[1.55]">
              {t('dashboard.subtitle')}
            </p>
          </div>

          {canViewUsers && (
            <div className="lg:text-right">
              <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted-foreground mb-1.5">
                {isTenantUser ? t('dashboard.myUsers') : t('dashboard.totalUsers')}
              </div>
              <div className="text-[64px] sm:text-[72px] font-extralight tracking-[-0.04em] leading-none font-display gradient-text font-feature-settings">
                {heroAnimated.toLocaleString()}
              </div>
            </div>
          )}
        </div>
      </section>

      {/* ─── Stats grid ─── */}
      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {canViewUsers && (
          <StatCard
            icon={Users}
            label={isTenantUser ? t('dashboard.myUsers') : t('dashboard.totalUsers')}
            value={users.length}
            delta={t('dashboard.delta30d')}
            tone="copper"
            spark="M0,28 L15,24 L30,20 L45,22 L60,16 L75,12 L90,10 L100,4"
          />
        )}
        {canViewRoles && (
          <StatCard
            icon={Shield}
            label={t('dashboard.activeRoles')}
            value={activeRoles.length}
            delta={t('dashboard.rolesEnabled', { active: activeRoles.length, total: roles.length })}
            tone="emerald"
            spark="M0,26 L15,22 L30,24 L45,18 L60,14 L75,16 L90,8 L100,6"
          />
        )}
        {canViewRoles && (
          <StatCard
            icon={TrendingUp}
            label={t('dashboard.totalRoles')}
            value={roles.length}
            delta={t('dashboard.delta7d')}
            tone="violet"
            spark="M0,28 L20,28 L40,18 L60,18 L80,8 L100,8"
          />
        )}
        <StatCard
          icon={Blocks}
          label={isTenantUser ? t('dashboard.myOrganization') : t('dashboard.platformStatus')}
          value={t('common.active')}
          delta={t('dashboard.allSystems')}
          tone="amber"
        />
        <Slot id="dashboard-cards" props={{}} />
      </section>

      {/* ─── Activity + Recent users ─── */}
      <section className="grid gap-4 lg:grid-cols-2">
        {canViewAuditLogs && (
          <div className="surface-glass rounded-2xl border border-border/40 shadow-card overflow-hidden">
            <div className="flex items-center justify-between px-6 pt-5 pb-3">
              <h2 className="text-[15px] font-semibold text-foreground tracking-tight font-display flex items-center gap-2">
                <span className="pulse-dot" />
                {t('dashboard.recentActivity')}
              </h2>
              <Button variant="ghost" size="sm" asChild>
                <Link to={ROUTES.AUDIT_LOGS.LIST}>
                  {t('dashboard.viewAll')}
                  <ArrowRight className="h-3.5 w-3.5" />
                </Link>
              </Button>
            </div>
            {auditLogs.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-12 px-6">
                {t('dashboard.noRecentActivity')}
              </p>
            ) : (
              <div className="divide-y divide-border/30">
                {auditLogs.map((log, i) => {
                  const ActionIcon = actionIcon(log.action);
                  const tone = actionTone(log.action);
                  return (
                    <div
                      key={log.id}
                      className="flex items-start gap-3 px-6 py-3 transition-colors duration-150 hover:bg-secondary/40"
                    >
                      <div
                        className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-white ${TONE_BG[tone]}`}
                      >
                        <ActionIcon className="h-3.5 w-3.5" strokeWidth={2} />
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2 flex-wrap text-[13px]">
                          <span className="font-medium text-foreground">{log.entityType}</span>
                          <span className="text-muted-foreground/50">·</span>
                          <span className="font-mono text-[11px] text-muted-foreground">{log.action}</span>
                        </div>
                        <p className="text-[11px] text-muted-foreground mt-0.5 font-mono">
                          {log.performedByName || log.performedBy || '—'}
                        </p>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        {i === 0 && (
                          <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[9px] font-bold uppercase tracking-[0.1em] bg-[color-mix(in_srgb,var(--color-accent-500)_10%,transparent)] text-[var(--color-accent-700)] dark:text-[var(--color-accent-300)] border border-[color-mix(in_srgb,var(--color-accent-500)_22%,transparent)]">
                            <span className="h-1 w-1 rounded-full bg-[var(--color-accent-500)]" /> {t('dashboard.liveBadge')}
                          </span>
                        )}
                        <span className="text-[11px] text-muted-foreground whitespace-nowrap font-mono">
                          {formatTimeAgo(log.performedAt)}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {canViewUsers && (
          <div className="surface-glass rounded-2xl border border-border/40 shadow-card overflow-hidden">
            <div className="flex items-center justify-between px-6 pt-5 pb-3">
              <h2 className="text-[15px] font-semibold text-foreground tracking-tight font-display">
                {t('dashboard.recentUsers')}
              </h2>
              <Button variant="ghost" size="sm" asChild>
                <Link to={ROUTES.USERS.LIST}>
                  {t('dashboard.viewAll')}
                  <ArrowRight className="h-3.5 w-3.5" />
                </Link>
              </Button>
            </div>
            {recentUsers.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-12 px-6">
                {t('dashboard.noRecentUsers')}
              </p>
            ) : (
              <div className="divide-y divide-border/30">
                {recentUsers.map((u) => (
                  <Link
                    key={u.id}
                    to={ROUTES.USERS.getDetail(u.id)}
                    className="flex items-center gap-3 px-6 py-3 transition-colors duration-150 hover:bg-secondary/40"
                  >
                    <UserAvatar firstName={u.firstName} lastName={u.lastName} size="sm" />
                    <div className="min-w-0 flex-1">
                      <p className="text-[13px] font-medium text-foreground truncate">
                        {u.firstName} {u.lastName}
                      </p>
                      <p className="text-[11px] text-muted-foreground truncate font-mono">{u.email}</p>
                    </div>
                    <div className="flex flex-col items-end gap-1 shrink-0">
                      {u.roles && u.roles.length > 0 && (
                        <div className="flex gap-1">
                          {u.roles.slice(0, 2).map((role) => (
                            <Badge key={role} variant="secondary" className="text-[10px] py-0">
                              {role}
                            </Badge>
                          ))}
                        </div>
                      )}
                      {u.createdAt && (
                        <span className="text-[10px] text-muted-foreground font-mono">
                          {formatTimeAgo(u.createdAt)}
                        </span>
                      )}
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>
        )}
      </section>

      {/* ─── Quick actions ─── */}
      <section>
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3">
          {t('dashboard.quickOverview')}
        </div>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <ActionCard
            icon={Users}
            tone="copper"
            disabled={!canViewUsers}
            title={t('dashboard.usersManagement')}
            description={t('dashboard.usersManagementDesc')}
            count={users.length}
            countLabel={t('dashboard.usersLabel')}
            href={ROUTES.USERS.LIST}
          />
          <ActionCard
            icon={Shield}
            tone="violet"
            disabled={!canViewRoles}
            title={t('dashboard.rolesPermissions')}
            description={t('dashboard.rolesPermissionsDesc')}
            count={roles.length}
            countLabel={t('dashboard.rolesLabel')}
            href={ROUTES.ROLES.LIST}
          />
          <ActionCard
            icon={ClipboardList}
            tone="emerald"
            disabled={!canViewAuditLogs}
            title={t('dashboard.systemSettings')}
            description={t('dashboard.systemSettingsDesc')}
            count={auditLogs.length > 0 ? t('dashboard.infiniteEvents') : 0}
            countLabel={t('dashboard.eventsLabel')}
            href={ROUTES.AUDIT_LOGS.LIST}
          />
        </div>
      </section>
    </div>
  );
}

function ActionCard({
  icon: Icon,
  tone,
  disabled,
  title,
  description,
  count,
  countLabel,
  href,
}: {
  icon: LucideIcon;
  tone: StatTone;
  disabled?: boolean;
  title: string;
  description: string;
  count: number | string;
  countLabel: string;
  href: string;
}) {
  const Wrapper: React.ElementType = disabled ? 'div' : Link;
  const wrapperProps = disabled ? {} : { to: href };

  return (
    <Wrapper
      {...wrapperProps}
      className={cn(
        'surface-glass rounded-2xl p-5 border border-border/40 group block',
        !disabled && 'hover-lift-card cursor-pointer',
        disabled && 'opacity-60',
      )}
    >
      <div className="flex items-start justify-between mb-4">
        <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-white ${TONE_BG[tone]}`}>
          <Icon className="h-[18px] w-[18px]" strokeWidth={2} />
        </div>
        {!disabled && (
          <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:text-primary transition-colors" />
        )}
      </div>
      <div className="text-[14px] font-semibold text-foreground mb-1 tracking-tight">{title}</div>
      <div className="text-[11.5px] text-muted-foreground leading-[1.55] mb-3">{description}</div>
      <div className="flex items-baseline gap-1.5 pt-3 border-t border-border/30">
        <span className="font-mono text-[18px] font-medium text-foreground">{typeof count === 'number' ? count.toLocaleString() : count}</span>
        <span className="text-[10px] uppercase tracking-[0.15em] text-muted-foreground font-bold">{countLabel}</span>
      </div>
    </Wrapper>
  );
}
