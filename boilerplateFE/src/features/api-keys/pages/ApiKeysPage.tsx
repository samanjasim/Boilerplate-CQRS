import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Key, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, ConfirmDialog, Pagination, getPersistedPageSize } from '@/components/common';
import { useApiKeys, useRevokeApiKey } from '../api';
import { CreateApiKeyDialog } from '../components/CreateApiKeyDialog';
import { ApiKeySecretDisplay } from '../components/ApiKeySecretDisplay';
import { PlatformKeysTab } from '../components/PlatformKeysTab';
import { TenantKeysTab } from '../components/TenantKeysTab';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { formatDateTime } from '@/utils/format';
import type { ApiKeyDto, CreateApiKeyResponse } from '../api';

export default function ApiKeysPage() {
  const { hasPermission } = usePermissions();

  const isPlatformAdmin = hasPermission(PERMISSIONS.ApiKeys.ViewPlatform);

  if (isPlatformAdmin) {
    return <PlatformAdminView />;
  }

  return <TenantUserView />;
}

/** Platform admin sees tabs: Platform Keys | Tenant Keys */
function PlatformAdminView() {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<'platform' | 'tenant'>('platform');

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('apiKeys.title')}
        subtitle={t('apiKeys.description')}
      />

      {/* Tabs */}
      <div className="flex gap-1 border-b border-border">
        <button
          onClick={() => setActiveTab('platform')}
          className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
            activeTab === 'platform'
              ? 'border-primary [color:var(--active-text)]'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          {t('apiKeys.platformKeys')}
        </button>
        <button
          onClick={() => setActiveTab('tenant')}
          className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
            activeTab === 'tenant'
              ? 'border-primary [color:var(--active-text)]'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          {t('apiKeys.tenantKeys')}
        </button>
      </div>

      {activeTab === 'platform' ? <PlatformKeysTab /> : <TenantKeysTab />}
    </div>
  );
}

/** Tenant user sees simple CRUD table for their keys */
function TenantUserView() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data, isLoading, isError } = useApiKeys({ pageNumber, pageSize });
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

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('apiKeys.title')} />
        <EmptyState icon={Key} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
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
        title={t('apiKeys.title')}
        subtitle={t('apiKeys.description')}
        actions={
          hasPermission(PERMISSIONS.ApiKeys.Create) ? (
            <Button onClick={() => setShowCreate(true)}>
              <Plus className="mr-2 h-4 w-4" />
              {t('apiKeys.create')}
            </Button>
          ) : undefined
        }
      />

      {apiKeys.length === 0 ? (
        <EmptyState
          icon={Key}
          title={t('apiKeys.emptyTitle')}
          description={t('apiKeys.emptyDescription')}
          action={
            hasPermission(PERMISSIONS.ApiKeys.Create)
              ? { label: t('apiKeys.create'), onClick: () => setShowCreate(true) }
              : undefined
          }
        />
      ) : (
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
                  {!key.isRevoked && hasPermission(PERMISSIONS.ApiKeys.Delete) && (
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={t('apiKeys.revoke')}
                      onClick={() => setRevokeTarget(key)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      <CreateApiKeyDialog open={showCreate} onOpenChange={setShowCreate} onCreated={handleCreated} />
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
    </div>
  );
}
