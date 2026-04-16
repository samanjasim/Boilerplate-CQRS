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
import {
  useCreateIntegrationConfig,
  useUpdateIntegrationConfig,
  useIntegrationConfig,
} from '../api';
import type {
  IntegrationConfigDto,
  IntegrationType,
} from '@/types/communication.types';

interface IntegrationSetupDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editConfig?: IntegrationConfigDto | null;
}

// Dynamic credential fields per integration type
const CREDENTIAL_FIELDS: Record<IntegrationType, { key: string; label: string; placeholder: string }[]> = {
  Slack: [
    { key: 'WebhookUrl', label: 'Webhook URL', placeholder: 'https://hooks.slack.com/services/...' },
  ],
  Telegram: [
    { key: 'BotToken', label: 'Bot Token', placeholder: '123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11' },
    { key: 'ChatId', label: 'Chat ID', placeholder: '-1001234567890' },
  ],
  Discord: [
    { key: 'WebhookUrl', label: 'Webhook URL', placeholder: 'https://discord.com/api/webhooks/...' },
  ],
  MicrosoftTeams: [
    { key: 'WebhookUrl', label: 'Webhook URL', placeholder: 'https://outlook.office.com/webhook/...' },
  ],
};

const INTEGRATION_TYPES: IntegrationType[] = ['Slack', 'Telegram', 'Discord', 'MicrosoftTeams'];

export function IntegrationSetupDialog({ open, onOpenChange, editConfig }: IntegrationSetupDialogProps) {
  const { t } = useTranslation();
  const isEditing = !!editConfig;

  const [step, setStep] = useState(1);
  const totalSteps = 3;

  // Form state
  const [selectedType, setSelectedType] = useState<IntegrationType | ''>('');
  const [displayName, setDisplayName] = useState('');
  const [credentials, setCredentials] = useState<Record<string, string>>({});

  // Queries & mutations
  const { data: detailData } = useIntegrationConfig(editConfig?.id ?? '');
  const createMutation = useCreateIntegrationConfig();
  const updateMutation = useUpdateIntegrationConfig();

  const credentialFields = useMemo(
    () => (selectedType ? CREDENTIAL_FIELDS[selectedType] : []),
    [selectedType]
  );

  // Reset form on open/close
  useEffect(() => {
    if (open) {
      if (isEditing && editConfig) {
        setSelectedType(editConfig.integrationType);
        setDisplayName(editConfig.displayName);
        setStep(2); // Skip type selection when editing
      } else {
        setSelectedType('');
        setDisplayName('');
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
      if (!selectedType) return;
      await createMutation.mutateAsync({
        integrationType: selectedType,
        displayName,
        credentials,
      });
    }
    onOpenChange(false);
  };

  const canProceedStep1 = !!selectedType;
  const canProceedStep2 = credentialFields.length > 0
    ? credentialFields.every((f) => isEditing || credentials[f.key]?.trim())
    : true;
  const canSubmit = displayName.trim().length > 0;

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {isEditing
              ? t('communication.integrations.setup.editTitle')
              : t('communication.integrations.setup.title')}
          </DialogTitle>
          <p className="text-sm text-muted-foreground">
            {t('communication.channels.setup.step', { current: step, total: totalSteps })}
          </p>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {/* Step 1: Select Integration Type */}
          {step === 1 && (
            <div className="space-y-2">
              <Label>{t('communication.integrations.setup.selectType')}</Label>
              <div className="grid grid-cols-2 gap-2">
                {INTEGRATION_TYPES.map((type) => (
                  <Button
                    key={type}
                    type="button"
                    variant={selectedType === type ? 'default' : 'outline'}
                    className="w-full"
                    onClick={() => {
                      setSelectedType(type);
                      setCredentials({});
                    }}
                  >
                    {t(`communication.integrations.types.${type}`)}
                  </Button>
                ))}
              </div>
            </div>
          )}

          {/* Step 2: Credentials */}
          {step === 2 && credentialFields.length > 0 && (
            <div className="space-y-3">
              <Label>{t('communication.integrations.setup.configureCredentials')}</Label>
              {credentialFields.map((field) => (
                <div key={field.key} className="space-y-1">
                  <Label className="text-sm">{field.label}</Label>
                  <Input
                    type="password"
                    placeholder={
                      isEditing && detailData?.data?.maskedCredentials?.[field.key]
                        ? detailData.data.maskedCredentials[field.key]
                        : field.placeholder
                    }
                    value={credentials[field.key] ?? ''}
                    onChange={(e) =>
                      setCredentials((prev) => ({ ...prev, [field.key]: e.target.value }))
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

          {/* Step 2 fallback: editing with masked credentials from detail */}
          {step === 2 && credentialFields.length === 0 && isEditing && detailData?.data?.maskedCredentials && (
            <div className="space-y-3">
              <Label>{t('communication.integrations.setup.configureCredentials')}</Label>
              {Object.keys(detailData.data.maskedCredentials).map((field) => (
                <div key={field} className="space-y-1">
                  <Label className="text-sm">{field}</Label>
                  <Input
                    type="password"
                    placeholder={detailData.data.maskedCredentials![field]}
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

          {/* Step 3: Display Name */}
          {step === 3 && (
            <div className="space-y-2">
              <Label>{t('communication.channels.fields.displayName')}</Label>
              <Input
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                placeholder={
                  selectedType
                    ? `My ${t(`communication.integrations.types.${selectedType}`)} Integration`
                    : ''
                }
              />
            </div>
          )}
        </div>

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
