export interface WorkflowDefinitionSummary {
  id: string;
  name: string;
  displayName: string | null;
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
  formFields?: FormFieldDefinition[] | null;
  parallel?: ParallelConfig | null;
  sla?: SlaConfig | null;
  uiPosition?: { x: number; y: number } | null;
}

export interface ParallelConfig {
  mode: string;
  assignees: AssigneeConfig[];
}

export interface SlaConfig {
  reminderAfterHours?: number | null;
  escalateAfterHours?: number | null;
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
  entityDisplayName: string | null;
  canResubmit: boolean;
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
  entityDisplayName: string | null;
  formFields?: FormFieldDefinition[] | null;
  groupId?: string | null;
  parallelTotal?: number | null;
  parallelCompleted?: number | null;
  isOverdue?: boolean;
  hoursOverdue?: number | null;
  isDelegated?: boolean;
  delegatedFromDisplayName?: string | null;
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
  formData?: Record<string, unknown> | null;
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
  entityDisplayName: string | null;
  canResubmit: boolean;
}

// Form field types
export interface FormFieldDefinition {
  name: string;
  label: string;
  type: 'text' | 'textarea' | 'number' | 'date' | 'select' | 'checkbox';
  required?: boolean;
  options?: SelectOption[];
  min?: number;
  max?: number;
  maxLength?: number;
  placeholder?: string;
  description?: string;
}

export interface SelectOption {
  value: string;
  label: string;
}

// Delegation types
export interface DelegationRule {
  id: string;
  toUserId: string;
  toDisplayName?: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

export interface CreateDelegationRequest {
  toUserId: string;
  startDate: string;
  endDate: string;
}

// Request types
export interface StartWorkflowRequest {
  entityType: string;
  entityId: string;
  definitionName: string;
  context?: Record<string, unknown>;
  entityDisplayName?: string;
}

export interface ExecuteTaskRequest {
  action: string;
  comment?: string;
  formData?: Record<string, unknown>;
}

export interface UpdateDefinitionRequest {
  displayName?: string;
  description?: string;
  statesJson?: string;
  transitionsJson?: string;
}

export interface BatchItemOutcome {
  taskId: string;
  status: 'Succeeded' | 'Failed' | 'Skipped';
  error: string | null;
  errorCode?: string | null;
  fieldErrors?: Record<string, string[]> | null;
}

export interface BatchExecuteResult {
  succeeded: number;
  failed: number;
  skipped: number;
  items: BatchItemOutcome[];
}

export interface BatchExecuteTasksRequest {
  taskIds: string[];
  action: string;
  comment?: string;
}

// ── Analytics ──────────────────────────────────────────────────────────────

export type AnalyticsWindow = 'SevenDays' | 'ThirtyDays' | 'NinetyDays' | 'AllTime';

export interface HeadlineMetrics {
  totalStarted: number;
  totalCompleted: number;
  totalCancelled: number;
  avgCycleTimeHours: number | null;
}

export interface StateMetric {
  stateName: string;
  medianDwellHours: number;
  p95DwellHours: number;
  visitCount: number;
}

export interface ActionRateMetric {
  stateName: string;
  action: string;
  count: number;
  percentage: number;
}

export interface InstanceCountPoint {
  bucket: string;
  started: number;
  completed: number;
  cancelled: number;
}

export interface StuckInstance {
  instanceId: string;
  entityDisplayName: string | null;
  currentState: string;
  startedAt: string;
  daysSinceStarted: number;
  currentAssigneeDisplayName: string | null;
}

export interface ApproverActivity {
  userId: string;
  userDisplayName: string | null;
  approvals: number;
  rejections: number;
  returns: number;
  avgResponseTimeHours: number | null;
}

export interface WorkflowAnalytics {
  definitionId: string;
  definitionName: string;
  window: AnalyticsWindow;
  windowStart: string;
  windowEnd: string;
  instancesInWindow: number;
  headline: HeadlineMetrics;
  statesByBottleneck: StateMetric[];
  actionRates: ActionRateMetric[];
  instanceCountSeries: InstanceCountPoint[];
  stuckInstances: StuckInstance[];
  approverActivity: ApproverActivity[];
}
