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
import { useFeatureFlags, useSetTenantOverride, useRemoveTenantOverride } from '../api';
import type { FeatureFlagDto } from '../api';
import { FeatureFlagsList } from '../components/FeatureFlagsList';
import { CreateFeatureFlagDialog } from '../components/CreateFeatureFlagDialog';
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

        <FeatureFlagsList
          flags={data?.data ?? []}
          pagination={data?.pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />

        <CreateFeatureFlagDialog open={createOpen} onOpenChange={setCreateOpen} />
      </div>
    );
  }

  // Tenant admin: read-only resolved flags with opt-out for non-system booleans
  return (
    <div className="space-y-6">
      <PageHeader
        title={t('featureFlags.title')}
        subtitle={t('featureFlags.tenantDescription')}
      />

      <TenantFlagsList
        flags={data?.data ?? []}
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
  tenantId,
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
  const setOverrideMutation = useSetTenantOverride();
  const removeOverrideMutation = useRemoveTenantOverride();

  const isBooleanFlag = (flag: FeatureFlagDto) =>
    flag.valueType === 'Boolean' || (flag.valueType as unknown) === 0;

  const handleOptOut = async (flag: FeatureFlagDto) => {
    // Toggle: if currently true, set override to false. If overridden to false, remove override.
    if (flag.tenantOverrideValue !== null) {
      await removeOverrideMutation.mutateAsync({ flagId: flag.id, tenantId });
    } else {
      await setOverrideMutation.mutateAsync({
        flagId: flag.id,
        tenantId,
        data: { value: 'false' },
      });
    }
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
              <TableCell className="text-end">
                {/* Only non-system boolean flags can be toggled by tenant admins */}
                {isBooleanFlag(flag) && !flag.isSystem && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleOptOut(flag)}
                    disabled={setOverrideMutation.isPending || removeOverrideMutation.isPending}
                  >
                    {flag.tenantOverrideValue !== null
                      ? t('featureFlags.removeOverride')
                      : t('featureFlags.optOut')}
                  </Button>
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
