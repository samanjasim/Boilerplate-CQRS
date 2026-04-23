import { memo } from 'react';
import { BaseEdge, EdgeLabelRenderer, getSmoothStepPath, type EdgeProps } from '@xyflow/react';
import type { TransitionEdge as TransitionEdgeType } from './hooks/useDesignerStore';

function TransitionEdgeInner({
  id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data, selected,
}: EdgeProps<TransitionEdgeType>) {
  const [path, labelX, labelY] = getSmoothStepPath({
    sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition,
  });

  const conditional = data?.condition != null;
  const auto = data?.type?.toLowerCase() === 'auto';

  const stroke = conditional ? 'hsl(var(--accent-foreground))' : 'hsl(var(--muted-foreground))';
  const dash = auto ? '6 3' : undefined;

  return (
    <>
      <BaseEdge
        id={id}
        path={path}
        style={{ stroke, strokeWidth: selected ? 2.5 : 1.5, strokeDasharray: dash }}
        markerEnd="url(#react-flow__arrowclosed)"
      />
      <EdgeLabelRenderer>
        <div
          style={{
            position: 'absolute',
            transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
            pointerEvents: 'all',
          }}
          className={[
            'px-1.5 py-0.5 rounded text-[11px] font-medium bg-background border',
            selected ? 'border-primary' : 'border-border',
            !data?.trigger ? 'text-destructive italic' : 'text-foreground',
          ].join(' ')}
        >
          {conditional ? 'ƒ ' : ''}{data?.trigger || '(set trigger)'}
        </div>
      </EdgeLabelRenderer>
    </>
  );
}

export const TransitionEdge = memo(TransitionEdgeInner);
