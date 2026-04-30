import { useCallback } from 'react';
import {
  ReactFlow, Background, Controls, MiniMap, ReactFlowProvider,
  type NodeTypes, type EdgeTypes, type OnSelectionChangeParams,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { useDesignerStore } from './hooks/useDesignerStore';
import { StateNode } from './StateNode';
import { TransitionEdge } from './TransitionEdge';

const nodeTypes: NodeTypes = { state: StateNode };
const edgeTypes: EdgeTypes = { transition: TransitionEdge };

interface Props {
  readOnly?: boolean;
}

function DesignerCanvasInner({ readOnly = false }: Props) {
  const nodes = useDesignerStore(s => s.nodes);
  const edges = useDesignerStore(s => s.edges);
  const onNodesChange = useDesignerStore(s => s.onNodesChange);
  const onEdgesChange = useDesignerStore(s => s.onEdgesChange);
  const onConnect = useDesignerStore(s => s.onConnect);
  const select = useDesignerStore(s => s.select);

  const onSelectionChange = useCallback((p: OnSelectionChangeParams) => {
    if (p.nodes[0]) select({ kind: 'state', name: p.nodes[0].id });
    else if (p.edges[0]) select({ kind: 'transition', id: p.edges[0].id });
    else select({ kind: 'empty' });
  }, [select]);

  return (
    <div className="h-full w-full">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={readOnly ? undefined : onNodesChange}
        onEdgesChange={readOnly ? undefined : onEdgesChange}
        onConnect={readOnly ? undefined : onConnect}
        onSelectionChange={onSelectionChange}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        nodesDraggable={!readOnly}
        nodesConnectable={!readOnly}
        elementsSelectable
        fitView
        fitViewOptions={{ padding: 0.2 }}
        proOptions={{ hideAttribution: true }}
      >
        <Background gap={14} size={1} color="hsl(var(--border))" />
        {!readOnly && <MiniMap pannable zoomable />}
        {!readOnly && <Controls showInteractive={false} />}
      </ReactFlow>
    </div>
  );
}

export function DesignerCanvas(props: Props) {
  return (
    <ReactFlowProvider>
      <DesignerCanvasInner {...props} />
    </ReactFlowProvider>
  );
}
