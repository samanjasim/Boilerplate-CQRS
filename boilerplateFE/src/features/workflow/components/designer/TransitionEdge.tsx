import { memo } from 'react';
import { BaseEdge, EdgeLabelRenderer, getSmoothStepPath, type EdgeProps } from '@xyflow/react';
import type { TransitionEdge as TransitionEdgeType } from './hooks/useDesignerStore';
import { useDesignerStore } from './hooks/useDesignerStore';

// Quadratic bezier curving to one side of the source→target line, so two
// opposing edges between the same nodes don't render on top of each other.
function bidirectionalPath(
  sourceX: number, sourceY: number, targetX: number, targetY: number, side: 1 | -1,
): [string, number, number] {
  const midX = (sourceX + targetX) / 2;
  const midY = (sourceY + targetY) / 2;
  const dx = targetX - sourceX;
  const dy = targetY - sourceY;
  const len = Math.hypot(dx, dy) || 1;
  const curvature = 60;
  const cx = midX + (-dy / len) * curvature * side;
  const cy = midY + (dx / len) * curvature * side;
  const path = `M ${sourceX},${sourceY} Q ${cx},${cy} ${targetX},${targetY}`;
  // Approximate midpoint of the quadratic for label placement.
  const lx = 0.25 * sourceX + 0.5 * cx + 0.25 * targetX;
  const ly = 0.25 * sourceY + 0.5 * cy + 0.25 * targetY;
  return [path, lx, ly];
}

function TransitionEdgeInner({
  id, source, target, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data, selected,
}: EdgeProps<TransitionEdgeType>) {
  // When an opposing edge exists between the same two states, render both
  // as curved beziers on opposite sides so paths and labels are distinct.
  // Deterministic: the lexicographically smaller source curves to the right.
  const hasCounter = useDesignerStore(s =>
    s.edges.some(e => e.source === target && e.target === source),
  );

  const [path, labelX, labelY] = hasCounter
    ? bidirectionalPath(sourceX, sourceY, targetX, targetY, source < target ? 1 : -1)
    : getSmoothStepPath({
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
