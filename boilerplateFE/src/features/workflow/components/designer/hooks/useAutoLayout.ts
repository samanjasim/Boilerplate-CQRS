import { useCallback } from 'react';
import dagre from 'dagre';
import type { StateNode, TransitionEdge } from './useDesignerStore';

const NODE_WIDTH = 220;
const NODE_HEIGHT = 80;

export function useAutoLayout() {
  return useCallback((nodes: StateNode[], edges: TransitionEdge[]): StateNode[] => {
    const g = new dagre.graphlib.Graph();
    g.setDefaultEdgeLabel(() => ({}));
    g.setGraph({ rankdir: 'TB', nodesep: 48, ranksep: 80 });

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
