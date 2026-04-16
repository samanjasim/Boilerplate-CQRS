import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import {
  useAvailableProviders,
  useCreateChannelConfig,
  useUpdateChannelConfig,
  useChannelConfig,
} from '../api';
import type {
  ChannelConfigDto,
  NotificationChannel,
  ChannelProvider,
  AvailableProviderDto,
} from '@/types/communication.types';

interface ChannelSetupDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editConfig?: ChannelConfigDto | null;
}

export function ChannelSetupDialog({ open, onOpenChange, editConfig }: ChannelSetupDialogProps) {
  const { t } = useTranslation();
  const isEditing = !!editConfig;

  const [step, setStep] = useState(1);
  const totalSteps = 3;

  // Form state
  const [selectedChannel, setSelectedChannel] = useState<NotificationChannel | ''>('');
  const [selectedProvider, setSelectedProvider] = useState<ChannelProvider | ''>('');
  const [displayName, setDisplayName] = useState('');
  const [isDefault, setIsDefault] = useState(false);
  const [credentials, setCredentials] = useState<Record<string, string>>({});

  // Queries & mutations
  const { data: providersData, isLoading: providersLoading } = useAvailableProviders();
  const { data: detailData } = useChannelConfig(editConfig?.id ?? '');
  const createMutation = useCreateChannelConfig();
  const updateMutation = useUpdateChannelConfig();

  const providers: AvailableProviderDto[] = providersData?.data ?? [];

  // Unique channels from available providers
  const availableChannels = useMemo(() => {
    const channels = new Set(providers.map((p) => p.channel));
    return Array.from(channels) as NotificationChannel[];
  }, [providers]);

  // Providers for the selected channel
  const channelProviders = useMemo(
    () => providers.filter((p) => p.channel === selectedChannel),
    [providers, selectedChannel]
  );

  // Selected provider's required fields
  const selectedProviderDef = useMemo(
    () => providers.find((p) => p.channel === selectedChannel && p.provider === selectedProvider),
    [providers, selectedChannel, selectedProvider]
  );

  // Reset form on open/close
  useEffect(() => {
    if (open) {
      if (isEditing && editConfig) {
        setSelectedChannel(editConfig.channel);
        setSelectedProvider(editConfig.provider);
        setDisplayName(editConfig.displayName);
        setIsDefault(editConfig.isDefault);
        setStep(2); // Skip channel/provider selection when editing
      } else {
        setSelectedChannel('');
        setSelectedProvider('');
        setDisplayName('');
        setIsDefault(false);
        setCredentials({});
        setStep(1);
      }
    }
  }, [open, isEditing, editConfig]);

  // Pre-fill masked credentials when editing
  useEffect(() => {
    if (isEditing && detailData?.data?.maskedCredentials) {
      const masked = detailData.data.maskedCredentials;
      const creds: Record<string, string> = {};
      for (const key of Object.keys(masked)) {
        creds[key] = ''; // empty = keep existing
      }
      setCredentials(creds);
    }
  }, [isEditing, detailData]);

  const handleSubmit = async () => {
    if (isEditing && editConfig) {
      await updateMutation.mutateAsync({
        id: editConfig.id,
        displayName,
        credentials,
      });
    } else {
      if (!selectedChannel || !selectedProvider) return;
      await createMutation.mutateAsync({
        channel: selectedChannel as NotificationChannel,
        provider: selectedProvider as ChannelProvider,
        displayName,
        credentials,
        isDefault,
      });
    }
    onOpenChange(false);
  };

  const canProceedStep1 = selectedChannel && selectedProvider;
  const canProceedStep2 = selectedProviderDef
    ? selectedProviderDef.requiredCredentialFields.every((f) => isEditing || credentials[f]?.trim())
    : true;
  const canSubmit = displayName.trim().length > 0;

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {isEditing
              ? t('communication.channels.setup.editTitle')
              : t('communication.channels.setup.title')}
          </DialogTitle>
          <p className="text-sm text-muted-foreground">
            {t('communication.channels.setup.step', { current: step, total: totalSteps })}
          </p>
        </DialogHeader>

        {providersLoading ? (
          <div className="flex justify-center py-8">
            <Spinner size="lg" />
          </div>
        ) : (
          <div className="space-y-4 py-4">
            {/* Step 1: Select Channel & Provider */}
            {step === 1 && (
              <>
                <div className="space-y-2">
                  <Label>{t('communication.channels.setup.selectChannel')}</Label>
                  <div className="grid grid-cols-3 gap-2">
                    {availableChannels.map((ch) => (
                      <Button
                        key={ch}
                        type="button"
                        variant={selectedChannel === ch ? 'default' : 'outline'}
                        className="w-full"
                        onClick={() => {
                          setSelectedChannel(ch);
                          setSelectedProvider('');
                        }}
                      >
                        {t(`communication.channels.channelNames.${ch}`)}
                      </Button>
                    ))}
                  </div>
                </div>

                {selectedChannel && (
                  <div className="space-y-2">
                    <Label>{t('communication.channels.setup.selectProvider')}</Label>
                    <div className="grid grid-cols-2 gap-2">
                      {channelProviders.map((p) => (
                        <Button
                          key={p.provider}
                          type="button"
                          variant={selectedProvider === p.provider ? 'default' : 'outline'}
                          className="w-full"
                          onClick={() => setSelectedProvider(p.provider)}
                        >
                          {p.displayName}
                        </Button>
                      ))}
                    </div>
                  </div>
                )}
              </>
            )}

            {/* Step 2: Credentials */}
            {step === 2 && selectedProviderDef && (
              <div className="space-y-3">
                <Label>{t('communication.channels.setup.configureCredentials')}</Label>
                {selectedProviderDef.requiredCredentialFields.map((field) => (
                  <div key={field} className="space-y-1">
                    <Label className="text-sm">{field}</Label>
                    <Input
                      type="password"
                      placeholder={
                        isEditing && detailData?.data?.maskedCredentials?.[field]
                          ? detailData.data.maskedCredentials[field]
                          : field
                      }
                      value={credentials[field] ?? ''}
                      onChange={(e) =>
                        setCredentials((prev) => ({ ...prev, [field]: e.target.value }))
                      }
                    />
                    {isEditing && (
                      <p className="text-xs text-muted-foreground">
                        Leave empty to keep existing value
                      </p>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* Step 2 fallback: if editing and no provider def loaded yet */}
            {step === 2 && !selectedProviderDef && isEditing && detailData?.data && (
              <div className="space-y-3">
                <Label>{t('communication.channels.setup.configureCredentials')}</Label>
                {Object.keys(detailData.data.maskedCredentials).map((field) => (
                  <div key={field} className="space-y-1">
                    <Label className="text-sm">{field}</Label>
                    <Input
                      type="password"
                      placeholder={detailData.data.maskedCredentials[field]}
                      value={credentials[field] ?? ''}
                      onChange={(e) =>
                        setCredentials((prev) => ({ ...prev, [field]: e.target.value }))
                      }
                    />
                    <p className="text-xs text-muted-foreground">
                      Leave empty to keep existing value
                    </p>
                  </div>
                ))}
              </div>
            )}

            {/* Step 3: Name & Default */}
            {step === 3 && (
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label>{t('communication.channels.fields.displayName')}</Label>
                  <Input
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    placeholder={t('communication.channels.setup.displayNamePlaceholder')}
                  />
                </div>

                {!isEditing && (
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={isDefault}
                      onChange={(e) => setIsDefault(e.target.checked)}
                      className="h-4 w-4 rounded border-border accent-primary"
                    />
                    <span className="text-sm">{t('communication.channels.fields.isDefault')}</span>
                  </label>
                )}
              </div>
            )}
          </div>
        )}

        <DialogFooter className="gap-2">
          {step > 1 && (
            <Button variant="outline" onClick={() => setStep((s) => s - 1)} disabled={isPending}>
              {t('common.previous')}
            </Button>
          )}

          {step < totalSteps ? (
            <Button
              onClick={() => setStep((s) => s + 1)}
              disabled={step === 1 ? !canProceedStep1 : !canProceedStep2}
            >
              {t('common.next')}
            </Button>
          ) : (
            <Button onClick={handleSubmit} disabled={!canSubmit || isPending}>
              {isPending ? t('common.saving') : isEditing ? t('common.save') : t('common.create')}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
