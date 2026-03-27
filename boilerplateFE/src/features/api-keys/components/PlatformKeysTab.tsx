import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { Key, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState, ConfirmDialog, Pagination, getPersistedPageSize } from '@/components/common';
import { useApiKeys, useRevokeApiKey } from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { ApiKeyDto, CreateApiKeyResponse } from '../api';
import { CreateApiKeyDialog } from './CreateApiKeyDialog';
import { ApiKeySecretDisplay } from './ApiKeySecretDisplay';

export function PlatformKeysTab() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data, isLoading } = useApiKeys({ pageNumber, pageSize, keyType: 'platform' });
  const revokeMutation = useRevokeApiKey();

  const [showCreate, setShowCreate] = useState(false);
  const [createdKey, setCreatedKey] = useState<CreateApiKeyResponse | null>(null);
  const [revokeTarget, setRevokeTarget] = useState<ApiKeyDto | null>(null);

  const apiKeys: ApiKeyDto[] = data?.data ?? [];
  const pagination = data?.pagination;

  const handleCreated = (response: CreateApiKeyResponse) => {
    setShowCreate(false);
    setCreatedKey(response);
  };

  const handleRevoke = async () => {
    if (!revokeTarget) return;
    await revokeMutation.mutateAsync(revokeTarget.id);
    setRevokeTarget(null);
  };

  if (apiKeys.length === 0 && !isLoading) {
    return (
      <>
        <EmptyState
          icon={Key}
          title={t('apiKeys.emptyPlatformTitle')}
          description={t('apiKeys.emptyPlatformDescription')}
          action={
            hasPermission(PERMISSIONS.ApiKeys.CreatePlatform)
              ? { label: t('apiKeys.createPlatformKey'), onClick: () => setShowCreate(true) }
              : undefined
          }
        />
        <CreateApiKeyDialog open={showCreate} onOpenChange={setShowCreate} onCreated={handleCreated} isPlatform />
        {createdKey && (
          <ApiKeySecretDisplay open={!!createdKey} onOpenChange={() => setCreatedKey(null)} response={createdKey} />
        )}
      </>
    );
  }

  return (
    <>
      {hasPermission(PERMISSIONS.ApiKeys.CreatePlatform) && (
        <div className="flex justify-end">
          <Button onClick={() => setShowCreate(true)}>
            <Plus className="mr-2 h-4 w-4" />
            {t('apiKeys.createPlatformKey')}
          </Button>
        </div>
      )}

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('apiKeys.name')}</TableHead>
            <TableHead>{t('apiKeys.prefix')}</TableHead>
            <TableHead>{t('apiKeys.scopes')}</TableHead>
            <TableHead>{t('apiKeys.status')}</TableHead>
            <TableHead>{t('apiKeys.lastUsed')}</TableHead>
            <TableHead>{t('apiKeys.expires')}</TableHead>
            <TableHead>{t('apiKeys.created')}</TableHead>
            <TableHead className="text-right">{t('common.actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {apiKeys.map((key) => (
            <TableRow key={key.id}>
              <TableCell className="font-medium text-foreground">{key.name}</TableCell>
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
                  ? <Badge variant="destructive">{t('apiKeys.statusRevoked')}</Badge>
                  : key.isExpired
                    ? <Badge variant="secondary">{t('apiKeys.statusExpired')}</Badge>
                    : <Badge variant="default">{t('apiKeys.statusActive')}</Badge>}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {key.lastUsedAt ? formatDateTime(key.lastUsedAt) : t('apiKeys.never')}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {key.expiresAt ? formatDateTime(key.expiresAt) : t('apiKeys.noExpiry')}
              </TableCell>
              <TableCell className="text-muted-foreground">{formatDateTime(key.createdAt)}</TableCell>
              <TableCell className="text-right">
                {!key.isRevoked && hasPermission(PERMISSIONS.ApiKeys.DeletePlatform) && (
                  <Button variant="ghost" size="icon" onClick={() => setRevokeTarget(key)}>
                    <Trash2 className="h-4 w-4 text-destructive" />
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
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      <CreateApiKeyDialog open={showCreate} onOpenChange={setShowCreate} onCreated={handleCreated} isPlatform />
      {createdKey && (
        <ApiKeySecretDisplay open={!!createdKey} onOpenChange={() => setCreatedKey(null)} response={createdKey} />
      )}
      <ConfirmDialog
        isOpen={!!revokeTarget}
        onClose={() => setRevokeTarget(null)}
        title={t('apiKeys.revokeTitle')}
        description={t('apiKeys.revokeDescription', { name: revokeTarget?.name })}
        confirmLabel={t('apiKeys.revokeConfirm')}
        onConfirm={handleRevoke}
        isLoading={revokeMutation.isPending}
        variant="danger"
      />
    </>
  );
}
