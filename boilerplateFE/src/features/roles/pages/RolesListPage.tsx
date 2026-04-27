import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ArrowRight, Layers, Plus, Shield, Users } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import {
  EmptyState,
  getPersistedPageSize,
  PageHeader,
  Pagination,
  StatCard,
} from '@/components/common';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';
import { useFeatureFlag, usePermissions } from '@/hooks';
import { useAuthStore } from '@/stores';
import { useRoles } from '../api';

/* ─── Sparkline paths ────────────────────────────────────────────────────── */
const SPARK_TOTAL  = 'M0,26 L15,22 L30,20 L45,18 L60,14 L75,12 L90,8 L100,6';
const SPARK_SYSTEM = 'M0,28 L25,28 L50,20 L75,20 L100,12';
const SPARK_CUSTOM = 'M0,28 L20,24 L40,26 L60,18 L80,16 L100,10';

/* ─── Role name accent ───────────────────────────────────────────────────── */
const ADMIN_NAMES = ['admin', 'superadmin', 'super admin', 'owner'];
function isAdminRole(name: string) {
  return ADMIN_NAMES.some((a) => name.toLowerCase().includes(a));
}

export default function RolesListPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const user = useAuthStore((state) => state.user);
  const isTenantUser = !!user?.tenantId;

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);

  const { data, isLoading, isError } = useRoles({ params: { pageNumber, pageSize } });
  const roles = data?.data ?? [];
  const pagination = data?.pagination;

  const totalRoles   = pagination?.totalCount ?? roles.length;
  const systemRoles  = roles.filter((r) => r.isSystemRole).length;
  const customRoles  = roles.filter((r) => !r.isSystemRole).length;

  const { isEnabled: customRolesEnabled } = useFeatureFlag('roles.tenant_custom_enabled');
  const canCreate =
    hasPermission(PERMISSIONS.Roles.Create) && (!isTenantUser || customRolesEnabled);

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('roles.title')} />
        <EmptyState
          icon={Shield}
          title={t('common.errorOccurred')}
          description={t('common.tryAgain')}
        />
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
          canCreate ? (
            <Link to={ROUTES.ROLES.CREATE}>
              <Button>
                <Plus className="h-4 w-4" />
                {t('roles.createRole')}
              </Button>
            </Link>
          ) : undefined
        }
      />

      {/* ─── Hero metric strip ─── */}
      <section className="grid gap-4 sm:grid-cols-3">
        <StatCard
          icon={Shield}
          label={t('roles.title')}
          value={totalRoles}
          tone="copper"
          variant="hero"
          spark={SPARK_TOTAL}
        />
        <StatCard
          icon={Layers}
          label={t('roles.system')}
          value={systemRoles}
          tone="violet"
          spark={SPARK_SYSTEM}
        />
        <StatCard
          icon={Users}
          label={t('roles.custom')}
          value={customRoles}
          tone="emerald"
          spark={SPARK_CUSTOM}
        />
      </section>

      {/* ─── Role cards grid ─── */}
      {roles.length === 0 ? (
        <EmptyState icon={Shield} title={t('common.noResults')} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {roles.map((role) => (
            <Link key={role.id} to={ROUTES.ROLES.getDetail(role.id)}>
              <Card variant="elevated" className="h-full border border-border/40">
                <CardContent className="py-5 px-5">
                  <div className="flex items-start justify-between mb-4">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl btn-primary-gradient glow-primary-sm">
                      <Shield className="h-[18px] w-[18px] text-white" strokeWidth={2} />
                    </div>
                    <div className="flex items-center gap-1.5">
                      {role.isSystemRole ? (
                        <Badge variant="outline" className="text-[10px] font-mono uppercase tracking-wide">
                          {t('roles.system')}
                        </Badge>
                      ) : (
                        <Badge variant="secondary" className="text-[10px] font-mono uppercase tracking-wide">
                          {t('roles.custom')}
                        </Badge>
                      )}
                    </div>
                  </div>

                  <h3
                    className={`font-semibold text-[15px] tracking-tight mb-1 ${
                      isAdminRole(role.name) ? 'gradient-text' : 'text-foreground'
                    }`}
                  >
                    {role.name}
                  </h3>

                  {role.description && (
                    <p className="text-[13px] text-muted-foreground line-clamp-2 leading-[1.5] mb-3">
                      {role.description}
                    </p>
                  )}

                  <div className="flex items-center justify-between mt-3 pt-3 border-t border-border/30">
                    <div className="flex items-center gap-3 text-[12px] text-muted-foreground">
                      <span className="flex items-center gap-1">
                        <Users className="h-3 w-3" />
                        {role.userCount}
                      </span>
                      <span className="flex items-center gap-1">
                        <Shield className="h-3 w-3" />
                        {role.permissions?.length ?? 0}
                      </span>
                    </div>
                    <ArrowRight className="h-3.5 w-3.5 text-muted-foreground/50 group-hover:text-primary transition-colors" />
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPageNumber(1);
          }}
        />
      )}
    </div>
  );
}
