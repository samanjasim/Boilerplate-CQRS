import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Bell, Mail, Monitor } from 'lucide-react';
import { isModuleActive } from '@/config/modules.config';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  useNotificationPreferences,
  useUpdateNotificationPreferences,
} from '@/features/notifications/api';
import type { NotificationPreference } from '@/types';

const notificationTypeLabels: Record<string, string> = {
  UserCreated: 'notifications.types.userCreated',
  UserInvited: 'notifications.types.userInvited',
  RoleChanged: 'notifications.types.roleChanged',
  PasswordChanged: 'notifications.types.passwordChanged',
  TenantCreated: 'notifications.types.tenantCreated',
  InvitationAccepted: 'notifications.types.invitationAccepted',
  LoginFromNewDevice: 'notifications.types.loginFromNewDevice',
  CommentMentioned: 'notifications.types.commentMentioned',
};

export function NotificationPreferences() {
  const { t } = useTranslation();
  const { data: preferences, isLoading } = useNotificationPreferences();
  const { mutate: updatePreferences, isPending } = useUpdateNotificationPreferences();
  const [localPrefs, setLocalPrefs] = useState<NotificationPreference[]>([]);

  useEffect(() => {
    if (preferences) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setLocalPrefs(preferences);
    }
  }, [preferences]);

  const togglePref = (type: string, field: 'emailEnabled' | 'inAppEnabled') => {
    setLocalPrefs((prev) =>
      prev.map((p) =>
        p.notificationType === type ? { ...p, [field]: !p[field] } : p
      )
    );
  };

  const handleSave = () => {
    updatePreferences(localPrefs);
  };

  if (isLoading) {
    return null;
  }

  return (
    <Card>
      <CardContent className="py-6">
        <div className="flex items-center gap-2 mb-4">
          <Bell className="h-5 w-5 text-primary" />
          <h3 className="text-lg font-semibold text-foreground">
            {t('notifications.preferences')}
          </h3>
        </div>
        <p className="text-sm text-muted-foreground mb-6">
          {t('notifications.preferencesDesc')}
        </p>

        <div className="space-y-1">
          {/* Header row */}
          <div className="grid grid-cols-[1fr_80px_80px] gap-4 px-3 py-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
            <span>{t('notifications.notificationType')}</span>
            <span className="text-center">
              <Mail className="h-4 w-4 mx-auto" />
            </span>
            <span className="text-center">
              <Monitor className="h-4 w-4 mx-auto" />
            </span>
          </div>

          {localPrefs.map((pref) => (
            <div
              key={pref.notificationType}
              className="grid grid-cols-[1fr_80px_80px] gap-4 items-center px-3 py-3 rounded-lg hover:bg-muted/50"
            >
              <span className="text-sm">
                {t(notificationTypeLabels[pref.notificationType] ?? pref.notificationType)}
              </span>
              <div className="flex justify-center">
                {pref.notificationType === 'CommentMentioned' && !isModuleActive('communication') ? (
                  <div className="relative group">
                    <button
                      type="button"
                      role="switch"
                      aria-checked={false}
                      aria-label={`${t(notificationTypeLabels[pref.notificationType] ?? pref.notificationType)} — ${t('notifications.emailRequiresCommunication')}`}
                      disabled
                      title={t('notifications.emailRequiresCommunication')}
                      className="relative inline-flex h-5 w-9 shrink-0 items-center rounded-full border-2 border-transparent bg-input opacity-50 cursor-not-allowed"
                    >
                      <span className="pointer-events-none block h-4 w-4 rounded-full bg-background shadow-lg ring-0 translate-x-0" />
                    </button>
                    <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-1.5 text-xs text-popover-foreground bg-popover border rounded-lg shadow-md opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity whitespace-nowrap pointer-events-none z-10">
                      {t('notifications.emailRequiresCommunication')}
                    </div>
                  </div>
                ) : (
                  <button
                    type="button"
                    role="switch"
                    aria-checked={pref.emailEnabled}
                    aria-label={`${t(notificationTypeLabels[pref.notificationType] ?? pref.notificationType)} — Email`}
                    onClick={() => togglePref(pref.notificationType, 'emailEnabled')}
                    className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ${
                      pref.emailEnabled ? 'bg-primary' : 'bg-input'
                    }`}
                  >
                    <span
                      className={`pointer-events-none block h-4 w-4 rounded-full bg-background shadow-lg ring-0 transition-transform ${
                        pref.emailEnabled ? 'translate-x-4' : 'translate-x-0'
                      }`}
                    />
                  </button>
                )}
              </div>
              <div className="flex justify-center">
                <button
                  type="button"
                  role="switch"
                  aria-checked={pref.inAppEnabled}
                  onClick={() => togglePref(pref.notificationType, 'inAppEnabled')}
                  className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ${
                    pref.inAppEnabled ? 'bg-primary' : 'bg-input'
                  }`}
                >
                  <span
                    className={`pointer-events-none block h-4 w-4 rounded-full bg-background shadow-lg ring-0 transition-transform ${
                      pref.inAppEnabled ? 'translate-x-4' : 'translate-x-0'
                    }`}
                  />
                </button>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-6">
          <Button onClick={handleSave} disabled={isPending}>
            {isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
