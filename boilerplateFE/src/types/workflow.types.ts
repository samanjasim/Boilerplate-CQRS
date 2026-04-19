export interface WorkflowDefinitionSummary {
  id: string;
  name: string;
  entityType: string;
  stepCount: number;
  isTemplate: boolean;
  isActive: boolean;
  sourceModule: string | null;
}

export interface WorkflowDefinitionDetail {
  id: string;
  name: string;
  entityType: string;
  isTemplate: boolean;
  isActive: boolean;
  sourceModule: string | null;
  states: WorkflowStateConfig[];
  transitions: WorkflowTransitionConfig[];
}

export interface WorkflowStateConfig {
  name: string;
  displayName: string;
  type: string;
  assignee?: AssigneeConfig | null;
  actions?: string[] | null;
  onEnter?: HookConfig[] | null;
  onExit?: HookConfig[] | null;
}

export interface WorkflowTransitionConfig {
  from: string;
  to: string;
  trigger: string;
  type: string;
  condition?: ConditionConfig | null;
}

export interface AssigneeConfig {
  strategy: string;
  parameters?: Record<string, unknown> | null;
  fallback?: AssigneeConfig | null;
}

export interface HookConfig {
  type: string;
  template?: string | null;
  to?: string | null;
  event?: string | null;
  action?: string | null;
}

export interface ConditionConfig {
  field: string;
  operator: string;
  value: unknown;
}

export interface WorkflowStatusSummary {
  instanceId: string;
  definitionId: string;
  definitionName: string;
  currentState: string;
  status: string;
  startedAt: string;
  startedByUserId: string;
}

export interface PendingTaskSummary {
  taskId: string;
  instanceId: string;
  definitionName: string;
  entityType: string;
  entityId: string;
  stepName: string;
  assigneeRole: string | null;
  createdAt: string;
  dueDate: string | null;
  availableActions: string[] | null;
}

export interface WorkflowStepRecord {
  fromState: string;
  toState: string;
  stepType: string;
  action: string;
  actorUserId: string | null;
  actorDisplayName: string | null;
  comment: string | null;
  timestamp: string;
  metadata: Record<string, unknown> | null;
}

export interface WorkflowInstanceSummary {
  instanceId: string;
  definitionId: string;
  definitionName: string;
  entityType: string;
  entityId: string;
  currentState: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  startedByUserId: string | null;
  startedByDisplayName: string | null;
}

// Request types
export interface StartWorkflowRequest {
  entityType: string;
  entityId: string;
  definitionName: string;
  context?: Record<string, unknown>;
}

export interface ExecuteTaskRequest {
  action: string;
  comment?: string;
}

export interface UpdateDefinitionRequest {
  displayName?: string;
  description?: string;
  statesJson?: string;
  transitionsJson?: string;
}
