import { useState, type ComponentType } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Bot, Check, Copy, Globe, Hash, User as UserIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { UserAvatar } from '@/components/common';
import { ROUTES } from '@/config';
import type { AuditLog } from '@/types';

interface AuditMetadataCardProps {
  log: AuditLog;
}

function splitDisplayName(name: string | null | undefined) {
  const parts = (name ?? '').trim().split(/\s+/).filter(Boolean);
  return {
    firstName: parts[0],
    lastName: parts.length > 1 ? parts[parts.length - 1] : undefined,
  };
}

function CopyableField({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: string;
  icon: ComponentType<{ className?: string }>;
}) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(value).then(() => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    });
  };

  return (
    <div className="group flex items-start gap-2">
      <Icon className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
      <div className="min-w-0 flex-1">
        <div className="text-xs text-muted-foreground">{label}</div>
        <div className="break-all font-mono text-xs">{value}</div>
      </div>
      <Button
        variant="ghost"
        size="icon"
        className="h-7 w-7 opacity-0 group-hover:opacity-100"
        onClick={handleCopy}
      >
        {copied ? <Check className="h-3 w-3 text-emerald-500" /> : <Copy className="h-3 w-3" />}
      </Button>
    </div>
  );
}

export function AuditMetadataCard({ log }: AuditMetadataCardProps) {
  const { t } = useTranslation();
  const hasAgentAttribution = !!log.agentPrincipalId;
  const actorName = log.performedByName ?? t('auditLogs.detail.unknownActor');
  const actorNameParts = splitDisplayName(log.performedByName);

  return (
    <Card>
      <CardContent className="space-y-4 pt-6">
        <div>
          <div className="mb-2 text-xs text-muted-foreground">{t('auditLogs.detail.actor')}</div>
          {log.performedBy ? (
            <div className="flex items-center gap-3">
              <UserAvatar {...actorNameParts} size="sm" />
              <div className="min-w-0">
                <div className="truncate text-sm font-medium">{actorName}</div>
                <div className="truncate font-mono text-xs text-muted-foreground">{log.performedBy}</div>
              </div>
            </div>
          ) : (
            <div className="text-sm italic text-muted-foreground">{t('auditLogs.detail.systemAction')}</div>
          )}
        </div>

        {hasAgentAttribution && (
          <div className="space-y-2 rounded-lg border border-[var(--color-violet-200)] bg-[var(--color-violet-50)]/40 p-3 dark:border-[var(--color-violet-800)] dark:bg-[var(--color-violet-950)]/40">
            <div className="flex items-center gap-2">
              <Bot className="h-4 w-4 text-[var(--color-violet-600)] dark:text-[var(--color-violet-400)]" />
              <span className="text-xs font-medium text-[var(--color-violet-700)] dark:text-[var(--color-violet-300)]">
                {t('auditLogs.detail.agentAction')}
              </span>
            </div>
            {log.onBehalfOfUserId && (
              <CopyableField label={t('auditLogs.detail.onBehalfOf')} value={log.onBehalfOfUserId} icon={UserIcon} />
            )}
            <CopyableField label={t('auditLogs.detail.agentPrincipal')} value={log.agentPrincipalId!} icon={Bot} />
            {log.agentRunId && (
              <CopyableField label={t('auditLogs.detail.agentRun')} value={log.agentRunId} icon={Hash} />
            )}
          </div>
        )}

        {log.ipAddress && (
          <CopyableField label={t('auditLogs.detail.ipAddress')} value={log.ipAddress} icon={Globe} />
        )}

        {log.correlationId && (
          <div className="space-y-1">
            <CopyableField label={t('auditLogs.detail.correlationId')} value={log.correlationId} icon={Hash} />
            <Link
              to={`${ROUTES.AUDIT_LOGS.LIST}?searchTerm=${encodeURIComponent(log.correlationId)}`}
              className="ms-6 text-xs text-primary hover:underline"
            >
              {t('auditLogs.detail.viewSameConversation')}
            </Link>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
