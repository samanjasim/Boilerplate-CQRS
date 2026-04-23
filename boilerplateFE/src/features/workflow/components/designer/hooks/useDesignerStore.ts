import { create } from 'zustand';
import { applyNodeChanges, applyEdgeChanges, MarkerType, type Node, type Edge, type NodeChange, type EdgeChange, type Connection } from '@xyflow/react';
import type { WorkflowStateConfig, WorkflowTransitionConfig } from '@/types/workflow.types';
import { validateDefinition, type ValidationIssue } from '../validation/designerSchema';

// @xyflow/react v12 requires node data to satisfy Record<string, unknown>.
// We intersect with it so the typed fields stay accessible while the constraint is met.
export type StateNodeData = WorkflowStateConfig & Record<string, unknown>;
export type StateNode = Node<StateNodeData, 'state'>;

// Helper: WorkflowStateConfig lacks an index signature so TS rejects direct
// assignment to StateNodeData. Route through unknown to satisfy the xyflow
// constraint without losing the typed fields at call sites.
const toNodeData = (s: WorkflowStateConfig): StateNodeData =>
  s as unknown as StateNodeData;

export type TransitionEdgeData = Omit<WorkflowTransitionConfig, 'from' | 'to'>;
export type TransitionEdge = Edge<TransitionEdgeData, 'transition'>;

type Selection =
  | { kind: 'state'; name: string }
  | { kind: 'transition'; id: string }
  | { kind: 'empty' };

interface DesignerState {
  nodes: StateNode[];
  edges: TransitionEdge[];
  isDirty: boolean;
  selection: Selection;
  issues: ValidationIssue[];
}

interface DesignerActions {
  load: (states: WorkflowStateConfig[], transitions: WorkflowTransitionConfig[]) => void;
  onNodesChange: (changes: NodeChange[]) => void;
  onEdgesChange: (changes: EdgeChange[]) => void;
  onConnect: (connection: Connection) => void;
  addState: (state: WorkflowStateConfig, position: { x: number; y: number }) => void;
  updateStateByName: (name: string, patch: Partial<WorkflowStateConfig>) => void;
  updateTransitionById: (id: string, patch: Partial<WorkflowTransitionConfig>) => void;
  select: (selection: Selection) => void;
  markClean: () => void;
  setNodesFromLayout: (positioned: StateNode[]) => void;
  toDefinition: () => { states: WorkflowStateConfig[]; transitions: WorkflowTransitionConfig[] };
}

export type DesignerStore = DesignerState & DesignerActions;

const edgeIdFor = (from: string, trigger: string) => `${from}__${trigger}`;

function recompute(state: DesignerState): Pick<DesignerState, 'issues'> {
  const { states, transitions } = toDefinitionFrom(state.nodes, state.edges);
  return { issues: validateDefinition(states, transitions) };
}

function toDefinitionFrom(nodes: StateNode[], edges: TransitionEdge[]) {
  const states: WorkflowStateConfig[] = nodes.map(n => ({
    ...n.data,
    uiPosition: { x: n.position.x, y: n.position.y },
  }));
  const transitions: WorkflowTransitionConfig[] = edges.map(e => ({
    from: e.source,
    to: e.target,
    trigger: e.data?.trigger ?? '',
    type: e.data?.type ?? 'Manual',
    condition: e.data?.condition ?? null,
  }));
  return { states, transitions };
}

export const useDesignerStore = create<DesignerStore>((set, get) => ({
  nodes: [],
  edges: [],
  isDirty: false,
  selection: { kind: 'empty' },
  issues: [],

  load: (states, transitions) => {
    const nodes: StateNode[] = states.map((s, i) => ({
      id: s.name,
      type: 'state',
      position: s.uiPosition ?? { x: 0, y: i * 140 },
      data: toNodeData(s),
    }));
    const edges: TransitionEdge[] = transitions.map(t => ({
      id: edgeIdFor(t.from, t.trigger),
      source: t.from,
      target: t.to,
      type: 'transition',
      markerEnd: { type: MarkerType.ArrowClosed },
      data: { trigger: t.trigger, type: t.type ?? 'Manual', condition: t.condition ?? null },
    }));
    set({
      nodes,
      edges,
      isDirty: false,
      selection: { kind: 'empty' },
      ...recompute({ nodes, edges, isDirty: false, selection: { kind: 'empty' }, issues: [] }),
    });
  },

  onNodesChange: (changes) => {
    const next = applyNodeChanges(changes, get().nodes) as unknown as StateNode[];
    // Mark dirty only on user-initiated drag/remove; ignore pure selection events.
    const mutated = changes.some(c => c.type === 'position' || c.type === 'remove' || c.type === 'add');
    set({
      nodes: next,
      isDirty: mutated ? true : get().isDirty,
      ...recompute({ ...get(), nodes: next }),
    });
  },

  onEdgesChange: (changes) => {
    const next = applyEdgeChanges(changes, get().edges) as TransitionEdge[];
    const mutated = changes.some(c => c.type === 'remove' || c.type === 'add');
    set({
      edges: next,
      isDirty: mutated ? true : get().isDirty,
      ...recompute({ ...get(), edges: next }),
    });
  },

  onConnect: (connection) => {
    if (!connection.source || !connection.target) return;
    const trigger = ''; // user will name it in the side panel
    const id = edgeIdFor(connection.source, trigger || `__tmp_${Date.now()}`);
    const newEdge: TransitionEdge = {
      id,
      source: connection.source,
      target: connection.target,
      type: 'transition',
      markerEnd: { type: MarkerType.ArrowClosed },
      data: { trigger, type: 'Manual', condition: null },
    };
    const edges = [...get().edges, newEdge];
    set({
      edges,
      isDirty: true,
      selection: { kind: 'transition', id },
      ...recompute({ ...get(), edges }),
    });
  },

  addState: (state, position) => {
    const node: StateNode = {
      id: state.name,
      type: 'state',
      position,
      data: toNodeData(state),
    };
    const nodes = [...get().nodes, node];
    set({
      nodes,
      isDirty: true,
      selection: { kind: 'state', name: state.name },
      ...recompute({ ...get(), nodes }),
    });
  },

  updateStateByName: (name, patch) => {
    const nodes = get().nodes.map(n =>
      n.id === name ? { ...n, id: patch.name ?? n.id, data: { ...n.data, ...patch } } : n);
    // If the name changed, rewrite edge endpoints too.
    let edges = get().edges;
    if (patch.name && patch.name !== name) {
      edges = edges.map(e => ({
        ...e,
        source: e.source === name ? patch.name! : e.source,
        target: e.target === name ? patch.name! : e.target,
      }));
    }
    set({
      nodes,
      edges,
      isDirty: true,
      selection: patch.name ? { kind: 'state', name: patch.name } : get().selection,
      ...recompute({ ...get(), nodes, edges }),
    });
  },

  updateTransitionById: (id, patch) => {
    const edges = get().edges.map(e => e.id === id
      ? { ...e, data: { ...(e.data ?? { trigger: '', type: 'Manual', condition: null }), ...patch } as TransitionEdgeData }
      : e);
    set({
      edges,
      isDirty: true,
      ...recompute({ ...get(), edges }),
    });
  },

  select: (selection) => set({ selection }),

  markClean: () => set({ isDirty: false }),

  setNodesFromLayout: (positioned) => set({
    nodes: positioned,
    ...recompute({ ...get(), nodes: positioned }),
  }),

  toDefinition: () => toDefinitionFrom(get().nodes, get().edges),
}));
