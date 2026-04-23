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
    g.setGraph({ rankdir: 'TB', nodesep: 60, ranksep: 120 });

    for (const n of nodes) g.setNode(n.id, { width: NODE_WIDTH, height: NODE_HEIGHT });
    for (const e of edges) g.setEdge(e.source, e.target);

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
