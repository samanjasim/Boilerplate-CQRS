import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config/api.config';
import type { ApiResponse, PaginatedResponse } from '@/types/api.types';
import type {
  StartWorkflowRequest,
  ExecuteTaskRequest,
  UpdateDefinitionRequest,
  CreateDelegationRequest,
  WorkflowDefinitionSummary,
  WorkflowDefinitionDetail,
  WorkflowInstanceSummary,
  WorkflowStatusSummary,
  WorkflowStepRecord,
  PendingTaskSummary,
  DelegationRule,
  BatchExecuteTasksRequest,
  BatchExecuteResult,
} from '@/types/workflow.types';

export const workflowApi = {
  // Definitions
  getDefinitions: (entityType?: string): Promise<WorkflowDefinitionSummary[]> =>
    apiClient
      .get<ApiResponse<WorkflowDefinitionSummary[]>>(API_ENDPOINTS.WORKFLOW.DEFINITIONS, { params: { entityType } })
      .then((r) => r.data.data),

  getDefinition: (id: string): Promise<WorkflowDefinitionDetail> =>
    apiClient
      .get<ApiResponse<WorkflowDefinitionDetail>>(API_ENDPOINTS.WORKFLOW.DEFINITION_DETAIL(id))
      .then((r) => r.data.data),

  cloneDefinition: (id: string): Promise<string> =>
    apiClient
      .post<ApiResponse<string>>(API_ENDPOINTS.WORKFLOW.DEFINITION_CLONE(id))
      .then((r) => r.data.data),

  updateDefinition: (id: string, data: UpdateDefinitionRequest): Promise<void> =>
    apiClient.put(API_ENDPOINTS.WORKFLOW.DEFINITION_DETAIL(id), data).then(() => undefined),

  // Instances
  startWorkflow: (data: StartWorkflowRequest): Promise<string> =>
    apiClient
      .post<ApiResponse<string>>(API_ENDPOINTS.WORKFLOW.INSTANCES, data)
      .then((r) => r.data.data),

  getInstances: (params: {
    entityType?: string;
    state?: string;
    status?: string;
    startedByUserId?: string;
    page?: number;
    pageSize?: number;
  }): Promise<WorkflowInstanceSummary[]> =>
    apiClient
      .get<ApiResponse<WorkflowInstanceSummary[]>>(API_ENDPOINTS.WORKFLOW.INSTANCES, { params })
      .then((r) => r.data.data),

  getStatus: (entityType: string, entityId: string): Promise<WorkflowStatusSummary> =>
    apiClient
      .get<ApiResponse<WorkflowStatusSummary>>(API_ENDPOINTS.WORKFLOW.INSTANCE_STATUS, {
        params: { entityType, entityId },
      })
      .then((r) => r.data.data),

  getHistory: (instanceId: string): Promise<WorkflowStepRecord[]> =>
    apiClient
      .get<ApiResponse<WorkflowStepRecord[]>>(API_ENDPOINTS.WORKFLOW.INSTANCE_HISTORY(instanceId))
      .then((r) => r.data.data),

  cancelWorkflow: (instanceId: string, reason?: string): Promise<void> =>
    apiClient
      .post(API_ENDPOINTS.WORKFLOW.INSTANCE_CANCEL(instanceId), { reason })
      .then(() => undefined),

  transitionWorkflow: (instanceId: string, trigger: string): Promise<boolean> =>
    apiClient
      .post<ApiResponse<boolean>>(API_ENDPOINTS.WORKFLOW.INSTANCE_TRANSITION(instanceId), { trigger })
      .then((r) => r.data.data),

  // Tasks — paginated (returns envelope with data[] + pagination)
  getPendingTasks: (params?: { page?: number; pageSize?: number }): Promise<PaginatedResponse<PendingTaskSummary>> =>
    apiClient
      .get<PaginatedResponse<PendingTaskSummary>>(API_ENDPOINTS.WORKFLOW.TASKS, { params })
      .then((r) => r.data),

  getPendingTaskCount: (): Promise<number> =>
    apiClient
      .get<ApiResponse<number>>(API_ENDPOINTS.WORKFLOW.TASKS_COUNT)
      .then((r) => r.data.data),

  executeTask: (taskId: string, data: ExecuteTaskRequest): Promise<boolean> =>
    apiClient
      .post<ApiResponse<boolean>>(API_ENDPOINTS.WORKFLOW.TASK_EXECUTE(taskId), data, {
        suppressValidationToast: true,
      })
      .then((r) => r.data.data),

  batchExecuteTasks: (data: BatchExecuteTasksRequest): Promise<BatchExecuteResult> =>
    apiClient
      .post<ApiResponse<BatchExecuteResult>>(API_ENDPOINTS.WORKFLOW.TASK_BATCH_EXECUTE, data)
      .then((r) => r.data.data),

  // Delegations
  getDelegations: (): Promise<DelegationRule[]> =>
    apiClient
      .get<ApiResponse<DelegationRule[]>>(API_ENDPOINTS.WORKFLOW.DELEGATIONS)
      .then((r) => r.data.data),

  getActiveDelegation: (): Promise<DelegationRule | null> =>
    apiClient
      .get<ApiResponse<DelegationRule | null>>(API_ENDPOINTS.WORKFLOW.DELEGATION_ACTIVE)
      .then((r) => r.data.data),

  createDelegation: (data: CreateDelegationRequest): Promise<string> =>
    apiClient
      .post<ApiResponse<string>>(API_ENDPOINTS.WORKFLOW.DELEGATIONS, data)
      .then((r) => r.data.data),

  cancelDelegation: (id: string): Promise<void> =>
    apiClient.delete(API_ENDPOINTS.WORKFLOW.DELEGATION_CANCEL(id)).then(() => undefined),
};
