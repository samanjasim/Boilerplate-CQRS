import { memo } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { AlertTriangle, Clock, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { StateNode as StateNodeType } from './hooks/useDesignerStore';
import { useDesignerStore } from './hooks/useDesignerStore';

const TYPE_COLOR: Record<string, string> = {
  Initial: 'bg-blue-500',
  HumanTask: 'bg-amber-500',
  SystemAction: 'bg-purple-500',
  Terminal: 'bg-emerald-500',
};

function StateNodeInner({ data, id, selected }: NodeProps<StateNodeType>) {
  const hasError = useDesignerStore(s => s.issues.some(i => i.path.startsWith(`states[`) && i.path.includes(id)));
  const color = TYPE_COLOR[data.type] ?? 'bg-muted';

  return (
    <div
      className={[
        'rounded-xl border bg-card shadow-card w-[220px] text-sm',
        selected ? 'ring-2 ring-primary' : 'border-border',
      ].join(' ')}
      data-state-type={data.type}
    >
      <div className={['h-1.5 rounded-t-xl', color].join(' ')} />
      <div className="p-3 space-y-1.5">
        <div className="flex items-start justify-between gap-1.5">
          <div className="min-w-0">
            <div className="font-semibold text-foreground truncate">
              {data.displayName || data.name}
            </div>
            <div className="text-[11px] text-muted-foreground truncate">{data.name}</div>
          </div>
          {hasError && (
            <AlertTriangle className="h-3.5 w-3.5 text-destructive shrink-0" aria-label="validation errors" />
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
              SLA
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
