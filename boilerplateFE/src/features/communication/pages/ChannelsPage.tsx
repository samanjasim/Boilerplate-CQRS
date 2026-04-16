import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDistanceToNow } from 'date-fns';
import { MessageSquare, Plus, Pencil, Trash2, Send, Star, Mail, Smartphone, Bell, MessageCircle, Inbox } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, ConfirmDialog } from '@/components/common';
import {
  useChannelConfigs,
  useDeleteChannelConfig,
  useTestChannelConfig,
  useSetDefaultChannelConfig,
} from '../api';
import { ChannelSetupDialog } from '../components/ChannelSetupDialog';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import type { ChannelConfigDto, NotificationChannel } from '@/types/communication.types';

const CHANNEL_ICONS: Record<NotificationChannel, typeof Mail> = {
  Email: Mail,
  Sms: Smartphone,
  Push: Bell,
  WhatsApp: MessageCircle,
  InApp: Inbox,
};

export default function ChannelsPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [setupOpen, setSetupOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ChannelConfigDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ChannelConfigDto | null>(null);

  const { data, isLoading, isError } = useChannelConfigs();
  const deleteMutation = useDeleteChannelConfig();
  const testMutation = useTestChannelConfig();
  const setDefaultMutation = useSetDefaultChannelConfig();

  const configs: ChannelConfigDto[] = data?.data ?? [];

  const canManage = hasPermission(PERMISSIONS.Communication.ManageChannels);

  // Group configs by channel type
  const grouped = configs.reduce<Record<NotificationChannel, ChannelConfigDto[]>>((acc, cfg) => {
    if (!acc[cfg.channel]) acc[cfg.channel] = [];
    acc[cfg.channel].push(cfg);
    return acc;
  }, {} as Record<NotificationChannel, ChannelConfigDto[]>);

  const channelOrder: NotificationChannel[] = ['Email', 'Sms', 'Push', 'WhatsApp', 'InApp'];

  const handleDelete = async () => {
    if (!deleteTarget) return;
    await deleteMutation.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('communication.channels.title')} />
        <EmptyState
          icon={MessageSquare}
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
        title={t('communication.channels.title')}
        subtitle={t('communication.channels.subtitle')}
        actions={
          canManage ? (
            <Button onClick={() => setSetupOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              {t('communication.channels.addChannel')}
            </Button>
          ) : undefined
        }
      />

      {configs.length === 0 ? (
        <EmptyState
          icon={MessageSquare}
          title={t('communication.channels.noChannels')}
          description={t('communication.channels.noChannelsDescription')}
          action={
            canManage
              ? { label: t('communication.channels.addChannel'), onClick: () => setSetupOpen(true) }
              : undefined
          }
        />
      ) : (
        <div className="space-y-8">
          {channelOrder.map((channel) => {
            const channelConfigs = grouped[channel];
            if (!channelConfigs || channelConfigs.length === 0) return null;

            const ChannelIcon = CHANNEL_ICONS[channel];

            return (
              <div key={channel} className="space-y-3">
                <div className="flex items-center gap-2">
                  <ChannelIcon className="h-5 w-5 text-muted-foreground" />
                  <h3 className="text-lg font-semibold text-foreground">{t(`communication.channels.channelNames.${channel}`)}</h3>
                  <Badge variant="secondary" className="text-xs">{channelConfigs.length}</Badge>
                </div>

                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {channelConfigs.map((cfg) => (
                    <Card key={cfg.id}>
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div className="space-y-1">
                            <CardTitle className="text-base flex items-center gap-2">
                              {cfg.displayName}
                              {cfg.isDefault && (
                                <Star className="h-4 w-4 fill-amber-400 text-amber-400" />
                              )}
                            </CardTitle>
                            <p className="text-sm text-muted-foreground">{cfg.provider}</p>
                          </div>
                          <Badge variant={STATUS_BADGE_VARIANT[cfg.status] ?? 'secondary'}>
                            {t(`communication.channels.status.${cfg.status}`)}
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
                              : '—'}
                          </div>

                          {/* Actions */}
                          {canManage && (
                            <div className="flex gap-1 pt-1">
                              <Button
                                variant="ghost"
                                size="sm"
                                title={t('communication.channels.testButton')}
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

                              {!cfg.isDefault && (
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => setDefaultMutation.mutate(cfg.id)}
                                  disabled={setDefaultMutation.isPending}
                                >
                                  <Star className="h-4 w-4" />
                                </Button>
                              )}

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
      <ChannelSetupDialog
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
        description={t('communication.channels.confirmDelete')}
        confirmLabel={t('common.delete')}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
        variant="danger"
      />
    </div>
  );
}
