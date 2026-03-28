import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ToggleRight, ShieldAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/common';
import { useFeatureFlags, useSetTenantOverride, useRemoveTenantOverride } from '@/features/feature-flags/api';
import type { FeatureFlagDto } from '@/features/feature-flags/api';

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

interface TenantFeatureFlagsTabProps {
  tenantId: string;
}

export function TenantFeatureFlagsTab({ tenantId }: TenantFeatureFlagsTabProps) {
  const { t } = useTranslation();
  const { data, isLoading } = useFeatureFlags({ tenantId, pageSize: 200 });
  const setOverrideMutation = useSetTenantOverride();
  const removeOverrideMutation = useRemoveTenantOverride();

  const [editingFlag, setEditingFlag] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');

  const flags: FeatureFlagDto[] = data?.data ?? [];

  const handleSetOverride = async (flag: FeatureFlagDto, value: string) => {
    await setOverrideMutation.mutateAsync({
      flagId: flag.id,
      tenantId,
      data: { value },
    });
    setEditingFlag(null);
  };

  const handleRemoveOverride = async (flag: FeatureFlagDto) => {
    await removeOverrideMutation.mutateAsync({
      flagId: flag.id,
      tenantId,
    });
  };

  const handleToggleBoolean = async (flag: FeatureFlagDto) => {
    const newValue = flag.resolvedValue === 'true' ? 'false' : 'true';
    await handleSetOverride(flag, newValue);
  };

  const startEditing = (flag: FeatureFlagDto) => {
    setEditingFlag(flag.id);
    setEditValue(flag.tenantOverrideValue ?? flag.defaultValue);
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (flags.length === 0) {
    return (
      <EmptyState
        icon={ToggleRight}
        title={t('featureFlags.emptyTitle')}
        description={t('featureFlags.emptyDescription')}
      />
    );
  }

  const isBooleanFlag = (flag: FeatureFlagDto) =>
    flag.valueType === 'Boolean' || (flag.valueType as unknown) === 0;

  return (
    <Card>
      <CardContent className="py-6 space-y-4">
        <div className="flex items-center gap-2">
          <ToggleRight className="h-5 w-5 text-muted-foreground" />
          <h3 className="text-lg font-semibold text-foreground">
            {t('featureFlags.tenantFlags')}
          </h3>
        </div>

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
                  <div className="flex items-center justify-end gap-1">
                    {flag.isSystem && (
                      <ShieldAlert className="h-4 w-4 text-muted-foreground" />
                    )}

                    {/* Boolean flags: toggle override */}
                    {isBooleanFlag(flag) && !flag.isSystem && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleToggleBoolean(flag)}
                        disabled={setOverrideMutation.isPending}
                      >
                        {flag.resolvedValue === 'true' ? t('featureFlags.disabled') : t('featureFlags.enabled')}
                      </Button>
                    )}

                    {/* Non-boolean flags: inline edit */}
                    {!isBooleanFlag(flag) && !flag.isSystem && (
                      <>
                        {editingFlag === flag.id ? (
                          <div className="flex items-center gap-1">
                            <Input
                              value={editValue}
                              onChange={(e) => setEditValue(e.target.value)}
                              className="h-8 w-32"
                            />
                            <Button
                              variant="default"
                              size="sm"
                              onClick={() => handleSetOverride(flag, editValue)}
                              disabled={setOverrideMutation.isPending || !editValue}
                            >
                              {t('common.save')}
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setEditingFlag(null)}
                            >
                              {t('common.cancel')}
                            </Button>
                          </div>
                        ) : (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => startEditing(flag)}
                          >
                            {t('featureFlags.setOverride')}
                          </Button>
                        )}
                      </>
                    )}

                    {/* Remove override button */}
                    {flag.tenantOverrideValue !== null && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive"
                        onClick={() => handleRemoveOverride(flag)}
                        disabled={removeOverrideMutation.isPending}
                      >
                        {t('featureFlags.removeOverride')}
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
