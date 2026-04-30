import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link2, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import {
  useIntegrationConfigs,
  useDeleteIntegrationConfig,
  useTestIntegrationConfig,
} from '../api';
import { IntegrationSetupDialog } from '../components/IntegrationSetupDialog';
import { IntegrationsStatusHero } from '../components/IntegrationsStatusHero';
import { IntegrationConfigCard } from '../components/IntegrationConfigCard';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
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

      <IntegrationsStatusHero configs={configs} />

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
                    <IntegrationConfigCard
                      key={cfg.id}
                      config={cfg}
                      canManage={canManage}
                      onTest={() => testMutation.mutate(cfg.id)}
                      onEdit={() => {
                        setEditTarget(cfg);
                        setSetupOpen(true);
                      }}
                      onDelete={() => setDeleteTarget(cfg)}
                      isTestPending={testMutation.isPending}
                    />
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
