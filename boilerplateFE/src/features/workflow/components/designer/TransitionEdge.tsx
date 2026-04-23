import { memo } from 'react';
import { BaseEdge, EdgeLabelRenderer, getSmoothStepPath, type EdgeProps } from '@xyflow/react';
import type { StateNode, TransitionEdge as TransitionEdgeType } from './hooks/useDesignerStore';
import { useDesignerStore } from './hooks/useDesignerStore';

// Must match the rendered StateNode width and approximate height. Used only
// for bidirectional-edge routing — the non-counter path still uses React
// Flow's own handle-derived endpoints.
const NODE_W = 220;
const NODE_H = 184;

// Route a counter-edge pair as two curves that each enter/exit from the short
// side of the node. React Flow only exposes a Bottom source handle and a Top
// target handle on StateNode, so an upward edge would otherwise wrap around
// both nodes (Bottom → Top) — visually disconnected from the return meaning.
// Here we re-attach the endpoints to the shortest vertical span (bottom→top
// going down, top→bottom going up) and curve to one side of center.
function bidirectionalPath(
  srcPos: { x: number; y: number },
  tgtPos: { x: number; y: number },
  side: 1 | -1,
): [string, number, number] {
  const goingDown = srcPos.y < tgtPos.y;
  // Anchor each endpoint off-center horizontally so a counter-pair enters/
  // exits opposite sides of both nodes. The arrow markers then point cleanly
  // down (forward) or up (return) rather than at an oblique angle.
  const ANCHOR_SHIFT = 55;
  const sx = srcPos.x + NODE_W / 2 + side * ANCHOR_SHIFT;
  const sy = goingDown ? srcPos.y + NODE_H : srcPos.y;
  const tx = tgtPos.x + NODE_W / 2 + side * ANCHOR_SHIFT;
  const ty = goingDown ? tgtPos.y : tgtPos.y + NODE_H;

  // Gentle outward bow so the edge reads as a distinct curve, not a stiff
  // vertical. The bow is absolute (not perpendicular-to-travel), so the pair
  // visibly splits left/right regardless of travel direction.
  const BOW = 24;
  const cx = (sx + tx) / 2 + side * BOW;
  const cy = (sy + ty) / 2;
  const path = `M ${sx},${sy} Q ${cx},${cy} ${tx},${ty}`;
  const lx = 0.25 * sx + 0.5 * cx + 0.25 * tx;
  const ly = 0.25 * sy + 0.5 * cy + 0.25 * ty;
  return [path, lx, ly];
}

const findNodePos = (nodes: StateNode[], id: string) =>
  nodes.find(n => n.id === id)?.position ?? null;

function TransitionEdgeInner({
  id, source, target, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data, selected,
}: EdgeProps<TransitionEdgeType>) {
  // When an opposing edge exists between the same two states, render both
  // as curved beziers on opposite sides so paths and labels are distinct.
  // Each selector returns a primitive/stable reference so Zustand's default
  // Object.is equality keeps the component from re-rendering in a loop.
  // Deterministic: the lexicographically smaller source curves to the right.
  const hasCounter = useDesignerStore(s =>
    s.edges.some(e => e.source === target && e.target === source),
  );
  const srcPos = useDesignerStore(s => (hasCounter ? findNodePos(s.nodes, source) : null));
  const tgtPos = useDesignerStore(s => (hasCounter ? findNodePos(s.nodes, target) : null));

  const [path, labelX, labelY] = hasCounter && srcPos && tgtPos
    ? bidirectionalPath(srcPos, tgtPos, source < target ? 1 : -1)
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
