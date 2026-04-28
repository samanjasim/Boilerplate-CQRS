import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { Key, AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState, Pagination, getPersistedPageSize } from '@/components/common';
import { useApiKeys } from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { ApiKeyDto } from '../api';
import { EmergencyRevokeDialog } from './EmergencyRevokeDialog';

export function TenantKeysTab() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data, isLoading } = useApiKeys({ pageNumber, pageSize, keyType: 'tenant' });

  const [emergencyTarget, setEmergencyTarget] = useState<ApiKeyDto | null>(null);

  const apiKeys: ApiKeyDto[] = data?.data ?? [];
  const pagination = data?.pagination;

  if (apiKeys.length === 0 && !isLoading) {
    return (
      <EmptyState
        icon={Key}
        title={t('apiKeys.emptyTenantTitle')}
        description={t('apiKeys.emptyTenantDescription')}
      />
    );
  }

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('apiKeys.tenant')}</TableHead>
            <TableHead>{t('apiKeys.name')}</TableHead>
            <TableHead>{t('apiKeys.prefix')}</TableHead>
            <TableHead>{t('apiKeys.scopes')}</TableHead>
            <TableHead>{t('apiKeys.status')}</TableHead>
            <TableHead>{t('apiKeys.lastUsed')}</TableHead>
            <TableHead>{t('apiKeys.created')}</TableHead>
            {hasPermission(PERMISSIONS.ApiKeys.EmergencyRevoke) && (
              <TableHead className="text-right">{t('common.actions')}</TableHead>
            )}
          </TableRow>
        </TableHeader>
        <TableBody>
          {apiKeys.map((key) => (
            <TableRow key={key.id}>
              <TableCell className="font-medium text-foreground">
                {key.tenantName ?? '-'}
              </TableCell>
              <TableCell className="text-foreground">{key.name}</TableCell>
              <TableCell>
                <code className="rounded-md bg-secondary px-2 py-1 text-xs text-muted-foreground">
                  {key.keyPrefix}...
                </code>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {key.scopes.map((scope) => (
                    <Badge key={scope} variant="outline" className="text-xs">{scope}</Badge>
                  ))}
                </div>
              </TableCell>
              <TableCell>
                {key.isRevoked
                  ? <Badge variant="failed">{t('apiKeys.statusRevoked')}</Badge>
                  : key.isExpired
                    ? <Badge variant="secondary">{t('apiKeys.statusExpired')}</Badge>
                    : <Badge variant="healthy">{t('apiKeys.statusActive')}</Badge>}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {key.lastUsedAt ? formatDateTime(key.lastUsedAt) : t('apiKeys.never')}
              </TableCell>
              <TableCell className="text-muted-foreground">{formatDateTime(key.createdAt)}</TableCell>
              {hasPermission(PERMISSIONS.ApiKeys.EmergencyRevoke) && (
                <TableCell className="text-right">
                  {!key.isRevoked && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-destructive hover:text-destructive"
                      onClick={() => setEmergencyTarget(key)}
                    >
                      <AlertTriangle className="mr-1 h-4 w-4" />
                      {t('apiKeys.emergencyRevoke')}
                    </Button>
                  )}
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      <EmergencyRevokeDialog
        open={!!emergencyTarget}
        onOpenChange={() => setEmergencyTarget(null)}
        apiKey={emergencyTarget}
      />
    </>
  );
}
