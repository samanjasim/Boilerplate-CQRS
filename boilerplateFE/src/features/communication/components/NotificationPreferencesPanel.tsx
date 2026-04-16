import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { cn } from '@/lib/utils';
import {
  useNotificationPreferences,
  useUpdateNotificationPreferences,
  useRequiredNotifications,
} from '../api';
import type { NotificationPreferenceItem, NotificationChannel } from '@/types/communication.types';

const CHANNELS: { key: keyof Omit<NotificationPreferenceItem, 'category'>; channel: NotificationChannel; label: string }[] = [
  { key: 'emailEnabled', channel: 'Email', label: 'Email' },
  { key: 'smsEnabled', channel: 'Sms', label: 'SMS' },
  { key: 'pushEnabled', channel: 'Push', label: 'Push' },
  { key: 'whatsAppEnabled', channel: 'WhatsApp', label: 'WhatsApp' },
  { key: 'inAppEnabled', channel: 'InApp', label: 'In-App' },
];

export function NotificationPreferencesPanel() {
  const { t } = useTranslation();
  const { data: prefsData, isLoading: prefsLoading } = useNotificationPreferences();
  const { data: requiredData, isLoading: requiredLoading } = useRequiredNotifications();
  const updateMutation = useUpdateNotificationPreferences();

  const [preferences, setPreferences] = useState<NotificationPreferenceItem[]>([]);

  const prefs = prefsData?.data ?? [];
  const requiredRules = requiredData?.data ?? [];

  useEffect(() => {
    if (prefs.length > 0) {
      setPreferences(
        prefs.map((p) => ({
          category: p.category,
          emailEnabled: p.emailEnabled,
          smsEnabled: p.smsEnabled,
          pushEnabled: p.pushEnabled,
          whatsAppEnabled: p.whatsAppEnabled,
          inAppEnabled: p.inAppEnabled,
        }))
      );
    }
  }, [prefsData]);

  const isRequired = (category: string, channel: NotificationChannel) =>
    requiredRules.some((r) => r.category === category && r.channel === channel);

  const toggleChannel = (categoryIndex: number, key: keyof Omit<NotificationPreferenceItem, 'category'>) => {
    setPreferences((prev) =>
      prev.map((p, i) => (i === categoryIndex ? { ...p, [key]: !p[key] } : p))
    );
  };

  const handleSave = () => {
    updateMutation.mutate(preferences);
  };

  if (prefsLoading || requiredLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Spinner className="h-6 w-6" />
      </div>
    );
  }

  if (preferences.length === 0) {
    return (
      <div className="text-center py-12 text-muted-foreground">
        {t('communication.preferences.subtitle')}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold">{t('communication.preferences.title')}</h3>
        <p className="text-sm text-muted-foreground">{t('communication.preferences.subtitle')}</p>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b">
              <th className="text-start py-3 pe-4 font-medium text-muted-foreground">
                {t('communication.triggerRules.fields.name')}
              </th>
              {CHANNELS.map((ch) => (
                <th key={ch.key} className="px-4 py-3 text-center font-medium text-muted-foreground">
                  {ch.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {preferences.map((pref, idx) => (
              <tr key={pref.category} className="border-b last:border-b-0">
                <td className="py-3 pe-4 font-medium capitalize">
                  {pref.category}
                </td>
                {CHANNELS.map((ch) => {
                  const required = isRequired(pref.category, ch.channel);
                  const checked = pref[ch.key] as boolean;
                  return (
                    <td key={ch.key} className="px-4 py-3 text-center">
                      <div className="flex flex-col items-center gap-1">
                        <button
                          type="button"
                          role="checkbox"
                          aria-checked={required ? true : checked}
                          onClick={() => {
                            if (!required) toggleChannel(idx, ch.key);
                          }}
                          disabled={required}
                          className={cn(
                            'flex h-4 w-4 shrink-0 items-center justify-center rounded border transition-colors',
                            (checked || required)
                              ? 'border-primary bg-primary text-primary-foreground'
                              : 'border-input bg-background hover:border-primary',
                            required && 'opacity-75 cursor-not-allowed'
                          )}
                        >
                          {(checked || required) && <Check className="h-3 w-3" strokeWidth={3} />}
                        </button>
                        {required && (
                          <Badge variant="secondary" className="text-[10px] px-1 py-0">
                            {t('communication.preferences.required')}
                          </Badge>
                        )}
                      </div>
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex justify-end">
        <Button onClick={handleSave} disabled={updateMutation.isPending}>
          {updateMutation.isPending ? (
            <Spinner className="me-2 h-4 w-4" />
          ) : (
            <Save className="me-2 h-4 w-4" />
          )}
          {t('common.save')}
        </Button>
      </div>
    </div>
  );
}
