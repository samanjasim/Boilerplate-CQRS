import { useEffect, useState } from 'react';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { X } from 'lucide-react';
import {
  useRegisteredEvents,
  useMessageTemplates,
  useCreateTriggerRule,
  useUpdateTriggerRule,
} from '../api';
import type { TriggerRuleDto, CreateTriggerRuleData } from '@/types/communication.types';

const AVAILABLE_CHANNELS = ['Email', 'Sms', 'Push', 'WhatsApp', 'InApp'] as const;
const RECIPIENT_MODES = ['event_user', 'role', 'specific'] as const;

interface TriggerRuleFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editRule?: TriggerRuleDto | null;
}

export function TriggerRuleFormDialog({ open, onOpenChange, editRule }: TriggerRuleFormDialogProps) {
  const { t } = useTranslation();
  const isEdit = !!editRule;

  const [name, setName] = useState('');
  const [eventName, setEventName] = useState('');
  const [messageTemplateId, setMessageTemplateId] = useState('');
  const [recipientMode, setRecipientMode] = useState('event_user');
  const [channelSequence, setChannelSequence] = useState<string[]>([]);
  const [delaySeconds, setDelaySeconds] = useState(0);

  const { data: eventsData } = useRegisteredEvents();
  const { data: templatesData } = useMessageTemplates();
  const createMutation = useCreateTriggerRule();
  const updateMutation = useUpdateTriggerRule();

  const events = eventsData?.data ?? [];
  const templates = templatesData?.data ?? [];

  useEffect(() => {
    if (open && editRule) {
      setName(editRule.name);
      setEventName(editRule.eventName);
      setMessageTemplateId(editRule.messageTemplateId);
      setRecipientMode(editRule.recipientMode);
      setChannelSequence([...editRule.channelSequence]);
      setDelaySeconds(editRule.delaySeconds);
    } else if (open) {
      setName('');
      setEventName('');
      setMessageTemplateId('');
      setRecipientMode('event_user');
      setChannelSequence([]);
      setDelaySeconds(0);
    }
  }, [open, editRule]);

  const handleChannelToggle = (channel: string) => {
    setChannelSequence((prev) =>
      prev.includes(channel)
        ? prev.filter((c) => c !== channel)
        : [...prev, channel]
    );
  };

  const handleRemoveChannel = (channel: string) => {
    setChannelSequence((prev) => prev.filter((c) => c !== channel));
  };

  const handleSubmit = async () => {
    const data: CreateTriggerRuleData = {
      name,
      eventName,
      messageTemplateId,
      recipientMode,
      channelSequence,
      delaySeconds,
    };

    if (isEdit && editRule) {
      await updateMutation.mutateAsync({ ...data, id: editRule.id });
    } else {
      await createMutation.mutateAsync(data);
    }

    onOpenChange(false);
  };

  const isPending = createMutation.isPending || updateMutation.isPending;
  const isValid = name.trim() && eventName && messageTemplateId && recipientMode && channelSequence.length > 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {isEdit
              ? t('communication.triggerRules.form.editTitle')
              : t('communication.triggerRules.form.title')}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {/* Name */}
          <div className="space-y-2">
            <Label>{t('communication.triggerRules.fields.name')}</Label>
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('communication.triggerRules.form.namePlaceholder')}
              maxLength={200}
            />
          </div>

          {/* Event */}
          <div className="space-y-2">
            <Label>{t('communication.triggerRules.fields.event')}</Label>
            <Select value={eventName} onValueChange={setEventName}>
              <SelectTrigger>
                <SelectValue placeholder={t('communication.triggerRules.form.selectEvent')} />
              </SelectTrigger>
              <SelectContent>
                {events.map((evt) => (
                  <SelectItem key={evt.id} value={evt.eventName}>
                    {evt.displayName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Template */}
          <div className="space-y-2">
            <Label>{t('communication.triggerRules.fields.template')}</Label>
            <Select value={messageTemplateId} onValueChange={setMessageTemplateId}>
              <SelectTrigger>
                <SelectValue placeholder={t('communication.triggerRules.form.selectTemplate')} />
              </SelectTrigger>
              <SelectContent>
                {templates.map((tpl) => (
                  <SelectItem key={tpl.id} value={tpl.id}>
                    {tpl.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Recipient Mode */}
          <div className="space-y-2">
            <Label>{t('communication.triggerRules.fields.recipientMode')}</Label>
            <Select value={recipientMode} onValueChange={setRecipientMode}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {RECIPIENT_MODES.map((mode) => (
                  <SelectItem key={mode} value={mode}>
                    {t(`communication.triggerRules.recipientModes.${mode}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Channel Sequence */}
          <div className="space-y-2">
            <Label>{t('communication.triggerRules.form.selectChannels')}</Label>
            <div className="flex flex-wrap gap-2">
              {AVAILABLE_CHANNELS.map((channel) => {
                const isSelected = channelSequence.includes(channel);
                return (
                  <button
                    key={channel}
                    type="button"
                    onClick={() => handleChannelToggle(channel)}
                    className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                      isSelected
                        ? 'bg-primary text-primary-foreground'
                        : 'bg-secondary text-secondary-foreground hover:bg-secondary/80'
                    }`}
                  >
                    {channel}
                  </button>
                );
              })}
            </div>
            {channelSequence.length > 0 && (
              <div className="flex flex-wrap gap-1.5 pt-1">
                <span className="text-xs text-muted-foreground">{t('communication.triggerRules.fields.channelSequence')}:</span>
                {channelSequence.map((ch, idx) => (
                  <Badge key={ch} variant="secondary" className="gap-1">
                    <span>{idx + 1}.</span> {ch}
                    <button
                      type="button"
                      onClick={() => handleRemoveChannel(ch)}
                      className="ml-0.5 hover:text-destructive"
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            )}
          </div>

          {/* Delay */}
          <div className="space-y-2">
            <Label>{t('communication.triggerRules.fields.delaySeconds')}</Label>
            <Input
              type="number"
              min={0}
              value={delaySeconds}
              onChange={(e) => setDelaySeconds(Number(e.target.value))}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            {t('common.cancel')}
          </Button>
          <Button onClick={handleSubmit} disabled={!isValid || isPending}>
            {isPending ? t('common.saving') : isEdit ? t('common.save') : t('common.create')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
