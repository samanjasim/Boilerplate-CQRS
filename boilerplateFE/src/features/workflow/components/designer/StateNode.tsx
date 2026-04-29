import { memo } from 'react';
import { useTranslation } from 'react-i18next';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { AlertTriangle, Clock, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import type { StateNode as StateNodeType } from './hooks/useDesignerStore';
import { useDesignerStore } from './hooks/useDesignerStore';

const TYPE_TINT: Record<string, { dot: string; ring: string; tooltipKey: string }> = {
  Initial: {
    dot: 'bg-emerald-500',
    ring: 'border-emerald-500/40 bg-emerald-50 ring-emerald-500/40 dark:bg-emerald-950/20',
    tooltipKey: 'workflow.designer.stateType.initial',
  },
  HumanTask: {
    dot: 'bg-primary',
    ring: 'border-primary/40 bg-[var(--active-bg)]/40 ring-primary/40',
    tooltipKey: 'workflow.designer.stateType.humanTask',
  },
  Final: {
    dot: 'bg-muted-foreground/60',
    ring: 'border-border bg-muted/40',
    tooltipKey: 'workflow.designer.stateType.final',
  },
  Terminal: {
    dot: 'bg-muted-foreground/60',
    ring: 'border-border bg-muted/40',
    tooltipKey: 'workflow.designer.stateType.final',
  },
};
const FALLBACK_TINT = {
  dot: 'bg-muted-foreground/40',
  ring: 'border-border bg-card',
  tooltipKey: 'workflow.designer.stateType.other',
};

function StateNodeInner({ data, id, selected }: NodeProps<StateNodeType>) {
  const { t } = useTranslation();
  // Match issue paths shaped like `states[<index>].*` against this node's index
  // in the store (issue paths are indexed, not named).
  const hasError = useDesignerStore(s => {
    const index = s.nodes.findIndex(n => n.id === id);
    if (index < 0) return false;
    const prefix = `states[${index}]`;
    return s.issues.some(i => i.path.startsWith(prefix));
  });
  const tint = TYPE_TINT[data.type] ?? FALLBACK_TINT;

  return (
    <div
      title={t(tint.tooltipKey)}
      className={cn(
        'w-[220px] rounded-xl border-2 text-sm shadow-card transition-shadow',
        tint.ring,
        selected && 'shadow-[var(--glow-primary-sm)] ring-2',
      )}
      data-state-type={data.type}
    >
      <div className="p-3 space-y-1.5">
        <div className="flex items-start justify-between gap-1.5">
          <div className="min-w-0">
            <div className="flex items-center gap-2 font-semibold text-foreground">
              <span className={cn('h-2 w-2 shrink-0 rounded-full', tint.dot)} />
              <span className="truncate">
                {data.displayName || data.name}
              </span>
            </div>
            <div className="text-[11px] text-muted-foreground truncate">
              {data.name}
            </div>
          </div>
          {hasError && (
            <AlertTriangle
              className="h-3.5 w-3.5 shrink-0 text-destructive"
              aria-label={t('workflow.designer.validationErrors')}
            />
          )}
        </div>

        <div className="flex items-center gap-1">
          <Badge variant="outline" className="text-[10px]">{data.type}</Badge>
          {data.assignee?.strategy && (
            <Badge variant="secondary" className="text-[10px] gap-1">
              <Users className="h-3 w-3" />
              {data.assignee.strategy}
              {data.assignee.parameters?.roleName ? `: ${String(data.assignee.parameters.roleName)}` : ''}
            </Badge>
          )}
          {data.sla && (data.sla.reminderAfterHours != null || data.sla.escalateAfterHours != null) && (
            <Badge variant="secondary" className="text-[10px] gap-1">
              <Clock className="h-3 w-3" />
              {t('workflow.sla.title')}
            </Badge>
          )}
        </div>

        {data.actions && data.actions.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {data.actions.map(a => (
              <span key={a} className="text-[10px] rounded bg-muted px-1.5 py-0.5">{a}</span>
            ))}
          </div>
        )}
      </div>

      <Handle type="target" position={Position.Top} className="!bg-primary" />
      <Handle type="source" position={Position.Bottom} className="!bg-primary" />
    </div>
  );
}

export const StateNode = memo(StateNodeInner);
