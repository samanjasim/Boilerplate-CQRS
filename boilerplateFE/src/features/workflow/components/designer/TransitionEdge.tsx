import { memo } from 'react';
import {
  BaseEdge,
  EdgeLabelRenderer,
  getSmoothStepPath,
  useInternalNode,
  type EdgeProps,
} from '@xyflow/react';
import type { TransitionEdge as TransitionEdgeType } from './hooks/useDesignerStore';
import { useDesignerStore } from './hooks/useDesignerStore';

// Route a counter-edge pair as two curves that each enter/exit from the short
// side of the node. React Flow only exposes a Bottom source handle and a Top
// target handle on StateNode, so an upward edge would otherwise wrap around
// both nodes (Bottom → Top) — visually disconnected from the return meaning.
// Here we re-attach the endpoints to the shortest vertical span (bottom→top
// going down, top→bottom going up) and curve to one side of center.
function bidirectionalPath(
  srcPos: { x: number; y: number },
  srcSize: { width: number; height: number },
  tgtPos: { x: number; y: number },
  tgtSize: { width: number; height: number },
  side: 1 | -1,
): [string, number, number] {
  const goingDown = srcPos.y < tgtPos.y;
  // Anchor each endpoint off-center horizontally so a counter-pair enters/
  // exits opposite sides of both nodes. The arrow markers then point cleanly
  // down (forward) or up (return) rather than at an oblique angle. The shift
  // is ~30% of the node width so endpoints land well inside the box edges.
  const shift = (w: number) => Math.min(55, w * 0.3);
  const sx = srcPos.x + srcSize.width / 2 + side * shift(srcSize.width);
  const sy = goingDown ? srcPos.y + srcSize.height : srcPos.y;
  const tx = tgtPos.x + tgtSize.width / 2 + side * shift(tgtSize.width);
  const ty = goingDown ? tgtPos.y : tgtPos.y + tgtSize.height;

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

function TransitionEdgeInner({
  id, source, target, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data, selected, markerEnd,
}: EdgeProps<TransitionEdgeType>) {
  // When an opposing edge exists between the same two states, render both
  // as curved beziers on opposite sides so paths and labels are distinct.
  // Deterministic: the lexicographically smaller source curves to the right.
  const hasCounter = useDesignerStore(s =>
    s.edges.some(e => e.source === target && e.target === source),
  );

  // Read measured dimensions from React Flow's internal node store so the
  // curve endpoints land exactly on the rendered box edges, regardless of
  // per-node content height (Initial ~75px vs HumanTask ~95px).
  const srcNode = useInternalNode(source);
  const tgtNode = useInternalNode(target);

  const canRouteBidi =
    hasCounter
    && srcNode?.measured?.width != null && srcNode.measured.height != null
    && tgtNode?.measured?.width != null && tgtNode.measured.height != null;

  const [path, labelX, labelY] = canRouteBidi
    ? bidirectionalPath(
        srcNode!.position,
        { width: srcNode!.measured!.width!, height: srcNode!.measured!.height! },
        tgtNode!.position,
        { width: tgtNode!.measured!.width!, height: tgtNode!.measured!.height! },
        source < target ? 1 : -1,
      )
    : getSmoothStepPath({
        sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition,
      });

  const conditional = data?.condition != null;
  const auto = data?.type?.toLowerCase() === 'auto';

  const stroke = selected ? 'hsl(var(--primary))' : 'hsl(var(--muted-foreground))';
  const dash = auto ? '6 3' : undefined;

  return (
    <>
      <BaseEdge
        id={id}
        path={path}
        style={{ stroke, strokeWidth: selected ? 2.5 : 1.5, strokeDasharray: dash }}
        markerEnd={markerEnd}
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
