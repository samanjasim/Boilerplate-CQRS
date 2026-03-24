import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Monitor, Globe, Clock, Trash2 } from 'lucide-react';
import { formatDateTime } from '@/utils/format';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common';
import { useSessions, useRevokeSession, useRevokeAllSessions } from '@/features/auth/api';

export function SessionsList() {
  const { t } = useTranslation();
  const { data: sessions, isLoading } = useSessions();
  const { mutate: revokeSession, isPending: isRevoking } = useRevokeSession();
  const { mutate: revokeAllSessions, isPending: isRevokingAll } = useRevokeAllSessions();

  const [confirmRevokeId, setConfirmRevokeId] = useState<string | null>(null);
  const [showRevokeAllConfirm, setShowRevokeAllConfirm] = useState(false);

  const hasOtherSessions = sessions && sessions.filter((s) => !s.isCurrent).length > 0;

  return (
    <Card>
      <CardContent className="py-6">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-lg font-semibold text-foreground">
            {t('sessions.title')}
          </h3>
          {hasOtherSessions && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowRevokeAllConfirm(true)}
              disabled={isRevokingAll}
            >
              <Trash2 className="h-4 w-4" />
              {t('sessions.revokeAll')}
            </Button>
          )}
        </div>

        {isLoading && (
          <p className="text-sm text-muted-foreground">{t('common.loading')}</p>
        )}

        {!isLoading && (!sessions || sessions.length === 0) && (
          <p className="text-sm text-muted-foreground">{t('sessions.noSessions')}</p>
        )}

        <div className="space-y-3">
          {sessions?.map((session) => (
            <div
              key={session.id}
              className="flex items-center justify-between rounded-lg border p-4"
            >
              <div className="flex items-start gap-3 min-w-0">
                <Monitor className="h-5 w-5 text-muted-foreground mt-0.5 shrink-0" />
                <div className="min-w-0 space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium">
                      {session.deviceInfo || t('sessions.device')}
                    </span>
                    {session.isCurrent && (
                      <Badge variant="default">{t('sessions.currentSession')}</Badge>
                    )}
                  </div>
                  {session.ipAddress && (
                    <div className="flex items-center gap-1 text-xs text-muted-foreground">
                      <Globe className="h-3 w-3" />
                      <span>{session.ipAddress}</span>
                    </div>
                  )}
                  <div className="flex items-center gap-3 text-xs text-muted-foreground">
                    <span className="flex items-center gap-1">
                      <Clock className="h-3 w-3" />
                      {t('sessions.lastActive')}: {formatDateTime(session.lastActiveAt)}
                    </span>
                    <span>
                      {t('sessions.createdAt')}: {formatDateTime(session.createdAt)}
                    </span>
                  </div>
                </div>
              </div>
              {!session.isCurrent && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-destructive hover:text-destructive shrink-0"
                  onClick={() => setConfirmRevokeId(session.id)}
                >
                  {t('sessions.revoke')}
                </Button>
              )}
            </div>
          ))}
        </div>

        <ConfirmDialog
          isOpen={confirmRevokeId !== null}
          onClose={() => setConfirmRevokeId(null)}
          onConfirm={() => {
            if (confirmRevokeId) {
              revokeSession(confirmRevokeId, {
                onSuccess: () => setConfirmRevokeId(null),
              });
            }
          }}
          title={t('sessions.revoke')}
          description={t('sessions.revokeConfirm')}
          isLoading={isRevoking}
        />

        <ConfirmDialog
          isOpen={showRevokeAllConfirm}
          onClose={() => setShowRevokeAllConfirm(false)}
          onConfirm={() => {
            revokeAllSessions(undefined, {
              onSuccess: () => setShowRevokeAllConfirm(false),
            });
          }}
          title={t('sessions.revokeAll')}
          description={t('sessions.revokeConfirm')}
          isLoading={isRevokingAll}
        />
      </CardContent>
    </Card>
  );
}
