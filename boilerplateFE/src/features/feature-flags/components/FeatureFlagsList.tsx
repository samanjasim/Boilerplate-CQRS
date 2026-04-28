import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ToggleRight, Pencil, Trash2, Layers } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState, ConfirmDialog, Pagination } from '@/components/common';
import { useDeleteFeatureFlag } from '../api';
import type { FeatureFlagDto } from '../api';
import { EditFeatureFlagDialog } from './EditFeatureFlagDialog';
import { TenantOverrideDialog } from './TenantOverrideDialog';
import { getFlagStatus } from '../utils/flag-status';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { PaginationMeta } from '@/types';

interface FeatureFlagsListProps {
  flags: FeatureFlagDto[];
  pagination?: PaginationMeta;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}

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

const FLAG_CATEGORY_LABEL_KEYS: Record<number, string> = {
  0: 'featureFlags.categoryUsers',
  1: 'featureFlags.categoryFiles',
  2: 'featureFlags.categoryReports',
  3: 'featureFlags.categoryApiKeys',
  4: 'featureFlags.categoryBilling',
  5: 'featureFlags.categorySystem',
  6: 'featureFlags.categoryCustom',
};

export function FeatureFlagsList({ flags, pagination, onPageChange, onPageSizeChange }: FeatureFlagsListProps) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const deleteMutation = useDeleteFeatureFlag();

  const [editTarget, setEditTarget] = useState<FeatureFlagDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<FeatureFlagDto | null>(null);
  const [overrideTarget, setOverrideTarget] = useState<FeatureFlagDto | null>(null);

  const handleDelete = async () => {
    if (!deleteTarget) return;
    await deleteMutation.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
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

  const hasActions =
    hasPermission(PERMISSIONS.FeatureFlags.Update) ||
    hasPermission(PERMISSIONS.FeatureFlags.Delete) ||
    hasPermission(PERMISSIONS.FeatureFlags.ManageTenantOverrides);

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('featureFlags.key')}</TableHead>
            <TableHead>{t('featureFlags.name')}</TableHead>
            <TableHead>{t('featureFlags.type')}</TableHead>
            <TableHead>{t('featureFlags.category')}</TableHead>
            <TableHead>{t('featureFlags.defaultValue')}</TableHead>
            <TableHead>{t('featureFlags.resolvedValue')}</TableHead>
            <TableHead>{t('featureFlags.status.title')}</TableHead>
            {hasActions && <TableHead className="text-end">{t('common.actions')}</TableHead>}
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
              <TableCell className="text-muted-foreground">
                {flag.category !== null ? t(FLAG_CATEGORY_LABEL_KEYS[flag.category] ?? 'featureFlags.categoryCustom') : '-'}
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">{flag.defaultValue}</TableCell>
              <TableCell>
                <div className="flex items-center gap-2">
                  {flag.valueType === 'Boolean' ? (
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
              {hasActions && (
                <TableCell className="text-end">
                  <div className="flex items-center justify-end gap-1">
                    {hasPermission(PERMISSIONS.FeatureFlags.ManageTenantOverrides) && (
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t('featureFlags.tenantOverride')}
                        onClick={() => setOverrideTarget(flag)}
                      >
                        <Layers className="h-4 w-4" />
                      </Button>
                    )}
                    {hasPermission(PERMISSIONS.FeatureFlags.Update) && (
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t('common.edit')}
                        onClick={() => setEditTarget(flag)}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                    )}
                    {hasPermission(PERMISSIONS.FeatureFlags.Delete) && !flag.isSystem && (
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t('common.delete')}
                        onClick={() => setDeleteTarget(flag)}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    )}
                  </div>
                </TableCell>
              )}
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

      <EditFeatureFlagDialog
        open={!!editTarget}
        onOpenChange={(open) => !open && setEditTarget(null)}
        flag={editTarget}
      />

      <TenantOverrideDialog
        open={!!overrideTarget}
        onOpenChange={(open) => !open && setOverrideTarget(null)}
        flag={overrideTarget}
      />

      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title={t('featureFlags.deleteTitle')}
        description={t('featureFlags.deleteDescription', { name: deleteTarget?.name })}
        confirmLabel={t('featureFlags.deleteConfirm')}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
        variant="danger"
      />
    </>
  );
}
