import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config/api.config';
import type { StartWorkflowRequest, ExecuteTaskRequest, UpdateDefinitionRequest, CreateDelegationRequest } from '@/types/workflow.types';

export const workflowApi = {
  // Definitions
  getDefinitions: (entityType?: string) =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.DEFINITIONS, { params: { entityType } }).then((r) => r.data),

  getDefinition: (id: string) =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.DEFINITION_DETAIL(id)).then((r) => r.data),

  cloneDefinition: (id: string) =>
    apiClient.post(API_ENDPOINTS.WORKFLOW.DEFINITION_CLONE(id)).then((r) => r.data),

  updateDefinition: (id: string, data: UpdateDefinitionRequest) =>
    apiClient.put(API_ENDPOINTS.WORKFLOW.DEFINITION_DETAIL(id), data).then((r) => r.data),

  // Instances
  startWorkflow: (data: StartWorkflowRequest) =>
    apiClient.post(API_ENDPOINTS.WORKFLOW.INSTANCES, data).then((r) => r.data),

  getInstances: (params: { entityType?: string; state?: string; status?: string; startedByUserId?: string; page?: number; pageSize?: number }) =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.INSTANCES, { params }).then((r) => r.data),

  getStatus: (entityType: string, entityId: string) =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.INSTANCE_STATUS, { params: { entityType, entityId } }).then((r) => r.data),

  getHistory: (instanceId: string) =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.INSTANCE_HISTORY(instanceId)).then((r) => r.data),

  cancelWorkflow: (instanceId: string, reason?: string) =>
    apiClient.post(API_ENDPOINTS.WORKFLOW.INSTANCE_CANCEL(instanceId), { reason }).then((r) => r.data),

  transitionWorkflow: (instanceId: string, trigger: string) =>
    apiClient.post(API_ENDPOINTS.WORKFLOW.INSTANCE_TRANSITION(instanceId), { trigger }).then((r) => r.data),

  // Tasks
  getPendingTasks: (params?: { page?: number; pageSize?: number }) =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.TASKS, { params }).then((r) => r.data),

  getPendingTaskCount: () =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.TASKS_COUNT).then((r) => r.data),

  executeTask: (taskId: string, data: ExecuteTaskRequest) =>
    apiClient.post(API_ENDPOINTS.WORKFLOW.TASK_EXECUTE(taskId), data).then((r) => r.data),

  // Delegations
  getDelegations: () =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.DELEGATIONS).then((r) => r.data),

  getActiveDelegation: () =>
    apiClient.get(API_ENDPOINTS.WORKFLOW.DELEGATION_ACTIVE).then((r) => r.data),

  createDelegation: (data: CreateDelegationRequest) =>
    apiClient.post(API_ENDPOINTS.WORKFLOW.DELEGATIONS, data).then((r) => r.data),

  cancelDelegation: (id: string) =>
    apiClient.delete(API_ENDPOINTS.WORKFLOW.DELEGATION_CANCEL(id)).then((r) => r.data),
};
