import { useCallback } from 'react';
import dagre from 'dagre';
import type { StateNode, TransitionEdge } from './useDesignerStore';

// Dagre uses these as the layout-space footprint per node. Rendered state
// nodes expand to ~180–200px tall depending on badges/actions, so NODE_HEIGHT
// is sized to the realistic max — otherwise dagre undersizes rank gaps and
// adjacent ranks overlap visually.
const NODE_WIDTH = 220;
const NODE_HEIGHT = 200;

export function useAutoLayout() {
  return useCallback((nodes: StateNode[], edges: TransitionEdge[]): StateNode[] => {
    const g = new dagre.graphlib.Graph();
    g.setDefaultEdgeLabel(() => ({}));
    // 'longest-path' ranker: each node is placed at its longest-path distance
    // from a source, so parallel terminals (e.g. Approved + Rejected from a
    // shared HumanTask parent) land at the same rank instead of stair-stepping
    // when the default network-simplex ranker breaks cycles like
    // ReturnForRevision (PendingReview → Draft).
    g.setGraph({ rankdir: 'TB', nodesep: 80, ranksep: 110, ranker: 'longest-path' });

    for (const n of nodes) g.setNode(n.id, { width: NODE_WIDTH, height: NODE_HEIGHT });

    // Skip back-edges (target depth < source depth in the forward DAG) when
    // building the layout graph. A back-edge like ReturnForRevision is a
    // legitimate transition but it pulls its source backwards in dagre's
    // ranking, dragging downstream siblings out of alignment.
    const forwardDepth = computeForwardDepth(nodes, edges);
    for (const e of edges) {
      const sourceDepth = forwardDepth.get(e.source) ?? 0;
      const targetDepth = forwardDepth.get(e.target) ?? 0;
      if (targetDepth < sourceDepth) continue;
      g.setEdge(e.source, e.target);
    }

    dagre.layout(g);

    return nodes.map(n => {
      const laid = g.node(n.id);
      return {
        ...n,
        position: { x: laid.x - NODE_WIDTH / 2, y: laid.y - NODE_HEIGHT / 2 },
      };
    });
  }, []);
}

/**
 * BFS depth from any zero-indegree "source" node. Used to identify back-edges
 * (where target depth < source depth) so they can be filtered out of the
 * dagre input — keeping parallel terminal states aligned at the same rank.
 */
function computeForwardDepth(nodes: StateNode[], edges: TransitionEdge[]): Map<string, number> {
  const adj = new Map<string, string[]>();
  const indegree = new Map<string, number>();
  for (const n of nodes) {
    adj.set(n.id, []);
    indegree.set(n.id, 0);
  }
  for (const e of edges) {
    adj.get(e.source)?.push(e.target);
    indegree.set(e.target, (indegree.get(e.target) ?? 0) + 1);
  }

  const depth = new Map<string, number>();
  const queue: string[] = [];
  for (const n of nodes) {
    if ((indegree.get(n.id) ?? 0) === 0) {
      depth.set(n.id, 0);
      queue.push(n.id);
    }
  }
  // Fallback: a graph that is one big cycle has no zero-indegree node. Seed
  // with the first node so we still produce a layout instead of returning all
  // depths as undefined.
  if (queue.length === 0 && nodes[0]) {
    depth.set(nodes[0].id, 0);
    queue.push(nodes[0].id);
  }

  while (queue.length > 0) {
    const id = queue.shift()!;
    const d = depth.get(id) ?? 0;
    for (const next of adj.get(id) ?? []) {
      const nextDepth = depth.get(next);
      if (nextDepth === undefined || nextDepth < d + 1) {
        depth.set(next, d + 1);
        queue.push(next);
      }
    }
  }
  return depth;
}
