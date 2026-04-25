import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDistanceToNow } from 'date-fns';
import { Link2, Plus, Pencil, Trash2, Send } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import {
  useIntegrationConfigs,
  useDeleteIntegrationConfig,
  useTestIntegrationConfig,
} from '../api';
import { IntegrationSetupDialog } from '../components/IntegrationSetupDialog';
import { usePermissions } from '@/hooks';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import type { IntegrationConfigDto, IntegrationType } from '@/types/communication.types';

const INTEGRATION_TYPE_ORDER: IntegrationType[] = ['Slack', 'Telegram', 'Discord', 'MicrosoftTeams'];

export default function IntegrationsPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [setupOpen, setSetupOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<IntegrationConfigDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<IntegrationConfigDto | null>(null);

  const { data, isLoading, isError } = useIntegrationConfigs();
  const deleteMutation = useDeleteIntegrationConfig();
  const testMutation = useTestIntegrationConfig();

  const configs: IntegrationConfigDto[] = data?.data ?? [];

  const canManage = hasPermission(PERMISSIONS.Communication.ManageIntegrations);

  // Group configs by integration type
  const grouped = configs.reduce<Record<IntegrationType, IntegrationConfigDto[]>>((acc, cfg) => {
    if (!acc[cfg.integrationType]) acc[cfg.integrationType] = [];
    acc[cfg.integrationType].push(cfg);
    return acc;
  }, {} as Record<IntegrationType, IntegrationConfigDto[]>);

  const handleDelete = async () => {
    if (!deleteTarget) return;
    await deleteMutation.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('communication.integrations.title')} />
        <EmptyState
          icon={Link2}
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
        title={t('communication.integrations.title')}
        subtitle={t('communication.integrations.subtitle')}
        actions={
          canManage ? (
            <Button onClick={() => setSetupOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              {t('communication.integrations.addIntegration')}
            </Button>
          ) : undefined
        }
      />

      {configs.length === 0 ? (
        <EmptyState
          icon={Link2}
          title={t('communication.integrations.noIntegrations')}
          description={t('communication.integrations.noIntegrationsDescription')}
          action={
            canManage
              ? { label: t('communication.integrations.addIntegration'), onClick: () => setSetupOpen(true) }
              : undefined
          }
        />
      ) : (
        <div className="space-y-8">
          {INTEGRATION_TYPE_ORDER.map((type) => {
            const typeConfigs = grouped[type];
            if (!typeConfigs || typeConfigs.length === 0) return null;

            return (
              <div key={type} className="space-y-3">
                <div className="flex items-center gap-2">
                  <Link2 className="h-5 w-5 text-muted-foreground" />
                  <h3 className="text-lg font-semibold text-foreground">
                    {t(`communication.integrations.types.${type}`)}
                  </h3>
                  <Badge variant="secondary" className="text-xs">{typeConfigs.length}</Badge>
                </div>

                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {typeConfigs.map((cfg) => (
                    <Card key={cfg.id}>
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div className="space-y-1">
                            <CardTitle className="text-base">{cfg.displayName}</CardTitle>
                            <p className="text-sm text-muted-foreground">
                              {t(`communication.integrations.types.${cfg.integrationType}`)}
                            </p>
                          </div>
                          <Badge variant={STATUS_BADGE_VARIANT[cfg.status] ?? 'secondary'}>
                            {cfg.status}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent>
                        <div className="space-y-3">
                          {/* Last tested */}
                          <div className="text-sm text-muted-foreground">
                            <span className="font-medium">{t('communication.channels.fields.lastTested')}:</span>{' '}
                            {cfg.lastTestedAt
                              ? formatDistanceToNow(new Date(cfg.lastTestedAt), { addSuffix: true })
                              : '\u2014'}
                          </div>

                          {/* Actions */}
                          {canManage && (
                            <div className="flex gap-1 pt-1">
                              <Button
                                variant="ghost"
                                size="sm"
                                title={t('communication.integrations.testButton')}
                                onClick={() => testMutation.mutate(cfg.id)}
                                disabled={testMutation.isPending}
                              >
                                <Send className="h-4 w-4" />
                              </Button>

                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => {
                                  setEditTarget(cfg);
                                  setSetupOpen(true);
                                }}
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>

                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setDeleteTarget(cfg)}
                              >
                                <Trash2 className="h-4 w-4 text-destructive" />
                              </Button>
                            </div>
                          )}
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Dialogs */}
      <IntegrationSetupDialog
        open={setupOpen}
        onOpenChange={(open) => {
          setSetupOpen(open);
          if (!open) setEditTarget(null);
        }}
        editConfig={editTarget}
      />

      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title={t('common.delete')}
        description={t('communication.integrations.confirmDelete')}
        confirmLabel={t('common.delete')}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
        variant="danger"
      />
    </div>
  );
}
