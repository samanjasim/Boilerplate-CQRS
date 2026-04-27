import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ArrowRight, Building, Building2, CircleOff } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import {
  EmptyState,
  getPersistedPageSize,
  PageHeader,
  Pagination,
  StatCard,
} from '@/components/common';
import { ROUTES } from '@/config';
import { type Tenant } from '@/types/tenant.types';
import { useTenants } from '../api';

/* ─── Sparkline paths ────────────────────────────────────────────────────── */
const SPARK_TOTAL     = 'M0,26 L15,22 L30,20 L45,18 L60,14 L75,10 L90,8 L100,4';
const SPARK_ACTIVE    = 'M0,24 L20,20 L40,18 L60,14 L80,10 L100,6';
const SPARK_SUSPENDED = 'M0,28 L25,26 L50,24 L75,26 L100,22';

/* ─── Status → J4 badge variant ─────────────────────────────────────────── */
function TenantStatusBadge({ status }: { status: string }) {
  const s = status.toLowerCase();
  if (s === 'active') return <Badge variant="healthy">{status}</Badge>;
  if (s === 'suspended') return <Badge variant="failed">{status}</Badge>;
  if (s === 'pending') return <Badge variant="pending">{status}</Badge>;
  return <Badge variant="outline">{status}</Badge>;
}

/* ─── Logo thumbnail ─────────────────────────────────────────────────────── */
function TenantLogo({ name, logoUrl }: { name: string; logoUrl: string | null }) {
  if (logoUrl) {
    return (
      <img
        src={logoUrl}
        alt={name}
        className="h-10 w-10 rounded-xl object-contain bg-background border border-border/40"
      />
    );
  }
  const initials = name
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0]?.toUpperCase() ?? '')
    .join('');
  return (
    <div className="h-10 w-10 rounded-xl btn-primary-gradient glow-primary-sm flex items-center justify-center text-white text-[13px] font-bold tracking-wide">
      {initials}
    </div>
  );
}

export default function TenantsListPage() {
  const { t } = useTranslation();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);

  const { data, isLoading, isError } = useTenants({ pageNumber, pageSize });
  const tenants = data?.data ?? [];
  const pagination = data?.pagination;

  const totalTenants    = pagination?.totalCount ?? tenants.length;
  const activeTenants   = (tenants as Tenant[]).filter((t) => t.status?.toLowerCase() === 'active').length;
  const suspendedTenants = (tenants as Tenant[]).filter((t) => t.status?.toLowerCase() === 'suspended').length;

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('tenants.title')} />
        <EmptyState
          icon={Building}
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
      <PageHeader title={t('tenants.title')} subtitle={t('tenants.allTenants')} />

      {/* ─── Hero metric strip ─── */}
      <section className="grid gap-4 sm:grid-cols-3">
        <StatCard
          icon={Building}
          label={t('tenants.title')}
          value={totalTenants}
          tone="copper"
          variant="hero"
          spark={SPARK_TOTAL}
        />
        <StatCard
          icon={Building2}
          label={t('common.active')}
          value={activeTenants}
          tone="emerald"
          spark={SPARK_ACTIVE}
        />
        <StatCard
          icon={CircleOff}
          label={t('common.suspended')}
          value={suspendedTenants}
          tone={suspendedTenants > 0 ? 'warn' : 'amber'}
          spark={SPARK_SUSPENDED}
        />
      </section>

      {/* ─── Tenant cards ─── */}
      {tenants.length === 0 ? (
        <EmptyState icon={Building} title={t('tenants.noTenants')} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {(tenants as Tenant[]).map((tenant) => (
            <Link key={tenant.id} to={ROUTES.TENANTS.getDetail(tenant.id)}>
              <Card variant="elevated" className="h-full border border-border/40">
                <CardContent className="py-5 px-5">
                  <div className="flex items-start justify-between mb-4">
                    <TenantLogo name={tenant.name} logoUrl={tenant.logoUrl} />
                    <TenantStatusBadge status={tenant.status} />
                  </div>

                  <h3 className="font-semibold text-[15px] tracking-tight text-foreground mb-0.5">
                    {tenant.name}
                  </h3>

                  {tenant.slug && (
                    <p className="text-[12px] text-muted-foreground font-mono">
                      /{tenant.slug}
                    </p>
                  )}

                  {tenant.description && (
                    <p className="text-[13px] text-muted-foreground line-clamp-2 leading-[1.5] mt-2">
                      {tenant.description}
                    </p>
                  )}

                  <div className="flex items-center justify-end mt-4 pt-3 border-t border-border/30">
                    <ArrowRight className="h-3.5 w-3.5 text-muted-foreground/50" />
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
