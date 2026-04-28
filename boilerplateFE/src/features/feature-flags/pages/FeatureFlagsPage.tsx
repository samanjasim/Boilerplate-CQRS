import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ToggleRight, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, getPersistedPageSize, Pagination } from '@/components/common';
import { useFeatureFlags, useOptOutFeatureFlag, useRemoveOptOut } from '../api';
import type { FeatureFlagDto } from '../api';
import { FeatureFlagsList } from '../components/FeatureFlagsList';
import { FeatureFlagStatStrip } from '../components/FeatureFlagStatStrip';
import { CreateFeatureFlagDialog } from '../components/CreateFeatureFlagDialog';
import { getFlagStatus } from '../utils/flag-status';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { useAuthStore, selectUser } from '@/stores';
import type { PaginationMeta } from '@/types';

const VALUE_TYPE_LABEL_KEYS: Record<string | number, string> = {
  0: 'featureFlags.boolean',
  1: 'featureFlags.string',
  2: 'featureFlags.integer',
  3: 'featureFlags.json',
  Boolean: 'featureFlags.boolean',
  String: 'featureFlags.string',
  Integer: 'featureFlags.integer',
  Json: 'featureFlags.json',
};

function getFeatureFlagStats(flags: FeatureFlagDto[]) {
  const isBooleanOn = (value: string) => value.toLowerCase() === 'true';
  const enabledCount = flags.filter((flag) =>
    flag.valueType === 'Boolean' ? isBooleanOn(flag.resolvedValue ?? flag.defaultValue) : false
  ).length;
  const tenantOverrideCount = flags.filter((flag) => flag.tenantOverrideValue !== null).length;
  const optedOutCount = flags.filter((flag) =>
    flag.valueType === 'Boolean' && flag.tenantOverrideValue?.toLowerCase() === 'false'
  ).length;

  return {
    enabledCount,
    tenantOverrideCount,
    optedOutCount,
  };
}

export default function FeatureFlagsPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);
  const isTenantUser = !!user?.tenantId;

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [createOpen, setCreateOpen] = useState(false);

  const queryParams: Record<string, unknown> = { pageNumber, pageSize };
  if (isTenantUser && user?.tenantId) {
    queryParams.tenantId = user.tenantId;
  }

  const { data, isLoading, isError } = useFeatureFlags(queryParams);

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('featureFlags.title')} />
        <EmptyState icon={ToggleRight} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
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

  // Platform admin: full CRUD view
  if (!isTenantUser) {
    const flags = data?.data ?? [];
    const stats = getFeatureFlagStats(flags);

    return (
      <div className="space-y-6">
        <PageHeader
          title={t('featureFlags.title')}
          subtitle={t('featureFlags.description')}
          actions={
            hasPermission(PERMISSIONS.FeatureFlags.Create) ? (
              <Button onClick={() => setCreateOpen(true)}>
                <Plus className="mr-2 h-4 w-4" />
                {t('featureFlags.create')}
              </Button>
            ) : undefined
          }
        />

        <FeatureFlagStatStrip
          enabledCount={stats.enabledCount}
          totalCount={flags.length}
          tenantOverrideCount={stats.tenantOverrideCount}
          optedOutCount={stats.optedOutCount}
        />

        <FeatureFlagsList
          flags={flags}
          pagination={data?.pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />

        <CreateFeatureFlagDialog open={createOpen} onOpenChange={setCreateOpen} />
      </div>
    );
  }

  // Tenant admin: read-only resolved flags with opt-out for non-system booleans
  const flags = data?.data ?? [];
  const stats = getFeatureFlagStats(flags);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('featureFlags.title')}
        subtitle={t('featureFlags.tenantDescription')}
      />

      <FeatureFlagStatStrip
        enabledCount={stats.enabledCount}
        totalCount={flags.length}
        tenantOverrideCount={stats.tenantOverrideCount}
        optedOutCount={stats.optedOutCount}
      />

      <TenantFlagsList
        flags={flags}
        tenantId={user!.tenantId!}
        pagination={data?.pagination}
        onPageChange={setPageNumber}
        onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
      />
    </div>
  );
}

/** Simplified flag list for tenant admins with opt-out toggles */
function TenantFlagsList({
  flags,
  pagination,
  onPageChange,
  onPageSizeChange,
}: {
  flags: FeatureFlagDto[];
  tenantId: string;
  pagination?: PaginationMeta;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}) {
  const { t } = useTranslation();
  const optOutMutation = useOptOutFeatureFlag();
  const removeOptOutMutation = useRemoveOptOut();

  const isBooleanFlag = (flag: FeatureFlagDto) =>
    flag.valueType === 'Boolean' || (flag.valueType as unknown) === 0;

  const handleOptOut = async (flag: FeatureFlagDto) => {
    await optOutMutation.mutateAsync(flag.id);
  };

  const handleRemoveOptOut = async (flag: FeatureFlagDto) => {
    await removeOptOutMutation.mutateAsync(flag.id);
  };

  if (flags.length === 0) {
    return (
      <EmptyState
        icon={ToggleRight}
        title={t('featureFlags.emptyTitle')}
        description={t('featureFlags.emptyDescription')}
      />
    );
  }

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('featureFlags.key')}</TableHead>
            <TableHead>{t('featureFlags.name')}</TableHead>
            <TableHead>{t('featureFlags.type')}</TableHead>
            <TableHead>{t('featureFlags.defaultValue')}</TableHead>
            <TableHead>{t('featureFlags.resolvedValue')}</TableHead>
            <TableHead>{t('featureFlags.status.title')}</TableHead>
            <TableHead className="text-end">{t('common.actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {flags.map((flag) => (
            <TableRow key={flag.id}>
              <TableCell className="font-medium text-foreground">
                <code className="rounded-md bg-secondary px-2 py-1 text-xs">{flag.key}</code>
              </TableCell>
              <TableCell className="text-foreground">{flag.name}</TableCell>
              <TableCell>
                <Badge variant="outline" className="text-xs">
                  {t(VALUE_TYPE_LABEL_KEYS[flag.valueType] ?? 'featureFlags.string')}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">{flag.defaultValue}</TableCell>
              <TableCell>
                <div className="flex items-center gap-2">
                  {isBooleanFlag(flag) ? (
                    <Badge variant={flag.resolvedValue === 'true' ? 'default' : 'secondary'}>
                      {flag.resolvedValue}
                    </Badge>
                  ) : (
                    <span className="text-sm text-foreground">{flag.resolvedValue}</span>
                  )}
                  {flag.tenantOverrideValue !== null && (
                    <Badge variant="outline" className="text-xs border-warning text-warning">
                      {t('featureFlags.overridden')}
                    </Badge>
                  )}
                </div>
              </TableCell>
              <TableCell>
                {(() => {
                  const status = getFlagStatus(flag, t);
                  return <Badge variant={status.variant}>{status.label}</Badge>;
                })()}
              </TableCell>
              <TableCell className="text-end">
                {/* Only non-system boolean flags can be opted out by tenant admins */}
                {isBooleanFlag(flag) && !flag.isSystem && (
                  <>
                    {flag.tenantOverrideValue === 'false' ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleRemoveOptOut(flag)}
                        disabled={removeOptOutMutation.isPending}
                      >
                        {t('featureFlags.removeOverride')}
                      </Button>
                    ) : flag.resolvedValue === 'true' ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleOptOut(flag)}
                        disabled={optOutMutation.isPending}
                      >
                        {t('featureFlags.optOut')}
                      </Button>
                    ) : null}
                  </>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={onPageChange}
          onPageSizeChange={onPageSizeChange}
        />
      )}
    </>
  );
}
