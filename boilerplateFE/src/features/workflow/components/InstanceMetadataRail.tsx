import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Calendar, Check, Copy, GitBranch, Layers, User } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ROUTES } from '@/config';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { cn } from '@/lib/utils';
import { formatDate, formatDateTime } from '@/utils/format';
import type { PendingTaskSummary, WorkflowInstanceSummary } from '@/types/workflow.types';
import { WorkflowStatusHeader, type StatusHeaderChip } from './WorkflowStatusHeader';

interface InstanceMetadataRailProps {
  instance: WorkflowInstanceSummary;
  myTask: PendingTaskSummary | null;
  isSuperAdmin: boolean;
  onAct: (task: PendingTaskSummary) => void;
  className?: string;
}

function CopyableField({
  label,
  value,
}: {
  label: string;
  value: string | null | undefined;
}) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  if (!value) return null;

  const handleCopy = () => {
    navigator.clipboard.writeText(value).then(() => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    });
  };

  return (
    <div className="group flex items-start justify-between gap-2 py-1.5">
      <div className="min-w-0 flex-1">
        <div className="text-[10px] uppercase tracking-wide text-muted-foreground/70">
          {label}
        </div>
        <div className="truncate font-mono text-xs font-medium text-foreground">{value}</div>
      </div>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        onClick={handleCopy}
        className="h-7 w-7 opacity-0 transition-opacity group-hover:opacity-100"
        aria-label={t('workflow.detail.metadata.copy')}
      >
        {copied ? <Check className="h-3 w-3 text-emerald-500" /> : <Copy className="h-3 w-3" />}
      </Button>
    </div>
  );
}

export function InstanceMetadataRail({
  instance,
  myTask,
  isSuperAdmin,
  onAct,
  className,
}: InstanceMetadataRailProps) {
  const { t } = useTranslation();
  const statusVariant = STATUS_BADGE_VARIANT[instance.status] ?? 'outline';
  const displayTitle = instance.entityDisplayName ?? `${instance.entityId.slice(0, 8)}...`;
  const tenantId =
    'tenantId' in instance
      ? (instance as WorkflowInstanceSummary & { tenantId?: string | null }).tenantId
      : null;

  const chips: StatusHeaderChip[] = [
    { icon: <GitBranch className="h-3 w-3" />, label: instance.definitionName, tinted: true },
    { icon: <Layers className="h-3 w-3" />, label: instance.entityType },
    ...(instance.currentState
      ? [{ label: instance.currentState, tinted: true }]
      : []),
    ...(instance.startedByDisplayName
      ? [{ icon: <User className="h-3 w-3" />, label: instance.startedByDisplayName }]
      : []),
    { icon: <Calendar className="h-3 w-3" />, label: formatDate(instance.startedAt) },
  ];

  return (
    <div
      className={cn('space-y-4 lg:sticky lg:self-start', className)}
      style={{ top: 'calc(var(--shell-header-h, 4rem) + 1.5rem)' }}
    >
      <WorkflowStatusHeader
        title={displayTitle}
        status={t(`workflow.status.${instance.status.toLowerCase()}`)}
        statusVariant={statusVariant}
        chips={chips}
      />

      {myTask && (
        <Card className="border-primary/30 bg-[var(--active-bg)]/30">
          <CardContent className="space-y-3 py-4">
            <div>
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('workflow.detail.pendingActionTitle')}
              </div>
              <div className="mt-1 text-sm font-semibold text-foreground">{myTask.stepName}</div>
            </div>
            <Button size="sm" onClick={() => onAct(myTask)} className="w-full">
              {t('workflow.inbox.actOn')}
            </Button>
          </CardContent>
        </Card>
      )}

      <Card variant="glass">
        <CardContent className="space-y-1 py-4">
          <CopyableField
            label={t('workflow.detail.metadata.instanceId')}
            value={instance.instanceId}
          />
          <CopyableField
            label={t('workflow.detail.metadata.entityId')}
            value={instance.entityId}
          />
          <Link
            to={ROUTES.WORKFLOWS.getDefinitionDetail(instance.definitionId)}
            className="inline-block pt-1 text-xs text-primary hover:underline"
          >
            {t('workflow.detail.metadata.definitionLink')}
          </Link>

          {instance.completedAt && (
            <div className="mt-2 border-t border-border pt-2">
              <div className="text-[10px] uppercase tracking-wide text-muted-foreground/70">
                {t('workflow.detail.completedAt')}
              </div>
              <div className="text-xs text-foreground">{formatDateTime(instance.completedAt)}</div>
            </div>
          )}

          {isSuperAdmin && tenantId && (
            <div className="mt-2 border-t border-border pt-2">
              <div className="text-[10px] uppercase tracking-wide text-muted-foreground/70">
                {t('workflow.detail.metadata.tenantId')}
              </div>
              <Badge
                variant="outline"
                className="border-[var(--active-border)] text-[var(--tinted-fg)]"
              >
                {tenantId}
              </Badge>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
