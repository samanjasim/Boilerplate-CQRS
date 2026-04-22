import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { queryKeys } from '@/lib/query/keys';
import { workflowApi } from './workflow.api';
import type { StartWorkflowRequest, ExecuteTaskRequest, UpdateDefinitionRequest, CreateDelegationRequest, BatchExecuteTasksRequest } from '@/types/workflow.types';
import { toast } from 'sonner';
import i18n from '@/i18n';

function handleMutationError(error: unknown) {
  const message =
    error instanceof AxiosError && error.response?.data?.message
      ? error.response.data.message
      : i18n.t('common.error', 'Something went wrong');
  toast.error(message);
}

// ── Queries ────────────────────────────────────────────────────────────────

export function useWorkflowDefinitions(entityType?: string) {
  return useQuery({
    queryKey: queryKeys.workflow.definitions.list(entityType),
    queryFn: () => workflowApi.getDefinitions(entityType),
  });
}

export function useWorkflowDefinition(id: string) {
  return useQuery({
    queryKey: queryKeys.workflow.definitions.detail(id),
    queryFn: () => workflowApi.getDefinition(id),
    enabled: !!id,
  });
}

export function useWorkflowStatus(entityType: string, entityId: string) {
  return useQuery({
    queryKey: queryKeys.workflow.instances.status(entityType, entityId),
    queryFn: () => workflowApi.getStatus(entityType, entityId),
    enabled: !!entityType && !!entityId,
  });
}

export function useWorkflowHistory(instanceId: string) {
  return useQuery({
    queryKey: queryKeys.workflow.instances.history(instanceId),
    queryFn: () => workflowApi.getHistory(instanceId),
    enabled: !!instanceId,
  });
}

export function useWorkflowInstances(params: { entityType?: string; state?: string; status?: string; startedByUserId?: string; page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: queryKeys.workflow.instances.list(params as Record<string, unknown>),
    queryFn: () => workflowApi.getInstances(params),
  });
}

export function usePendingTasks(params?: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: queryKeys.workflow.tasks.list(params as Record<string, unknown> | undefined),
    queryFn: () => workflowApi.getPendingTasks(params),
  });
}

export function usePendingTaskCount(enabled = true) {
  return useQuery({
    queryKey: queryKeys.workflow.tasks.count(),
    queryFn: () => workflowApi.getPendingTaskCount(),
    enabled,
  });
}

// ── Mutations ──────────────────────────────────────────────────────────────

export function useStartWorkflow() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: StartWorkflowRequest) => workflowApi.startWorkflow(data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });
      queryClient.invalidateQueries({
        queryKey: queryKeys.workflow.instances.status(variables.entityType, variables.entityId),
      });
      toast.success(i18n.t('workflow.workflowStarted', 'Workflow started'));
    },
    onError: handleMutationError,
  });
}

export function useExecuteTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ taskId, data }: { taskId: string; data: ExecuteTaskRequest }) =>
      workflowApi.executeTask(taskId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });
      toast.success(i18n.t('workflow.taskExecuted', 'Task completed'));
    },
    onError: handleMutationError,
  });
}

export function useBatchExecuteTasks() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: BatchExecuteTasksRequest) => workflowApi.batchExecuteTasks(data),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });

      const summary = {
        succeeded: result.succeeded,
        failed: result.failed,
        skipped: result.skipped,
      };
      const message = i18n.t('workflow.inbox.bulkResultSummary', summary);

      if (result.failed > 0 && result.succeeded > 0) toast.warning(message);
      else if (result.failed > 0) toast.error(message);
      else toast.success(message);
    },
    onError: handleMutationError,
  });
}

export function useCancelWorkflow() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ instanceId, reason }: { instanceId: string; reason?: string }) =>
      workflowApi.cancelWorkflow(instanceId, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });
      toast.success(i18n.t('workflow.workflowCancelled', 'Workflow cancelled'));
    },
    onError: handleMutationError,
  });
}

export function useTransitionWorkflow() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ instanceId, trigger }: { instanceId: string; trigger: string }) =>
      workflowApi.transitionWorkflow(instanceId, trigger),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.all });
      toast.success(i18n.t('workflow.resubmitSuccess', 'Workflow resubmitted'));
    },
    onError: handleMutationError,
  });
}

export function useCloneDefinition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => workflowApi.cloneDefinition(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.definitions.all });
      toast.success(i18n.t('workflow.definitionCloned', 'Workflow definition cloned'));
    },
    onError: handleMutationError,
  });
}

// ── Delegation Queries ────────────────────────────────────────────────────

export function useDelegations() {
  return useQuery({
    queryKey: queryKeys.workflow.delegations.list(),
    queryFn: () => workflowApi.getDelegations(),
  });
}

export function useActiveDelegation() {
  return useQuery({
    queryKey: queryKeys.workflow.delegations.active(),
    queryFn: () => workflowApi.getActiveDelegation(),
  });
}

export function useCreateDelegation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateDelegationRequest) => workflowApi.createDelegation(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.delegations.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.all });
      toast.success(i18n.t('workflow.delegation.success'));
    },
    onError: handleMutationError,
  });
}

export function useCancelDelegation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => workflowApi.cancelDelegation(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.delegations.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.all });
      toast.success(i18n.t('workflow.delegation.cancelled'));
    },
    onError: handleMutationError,
  });
}

export function useUpdateDefinition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateDefinitionRequest }) =>
      workflowApi.updateDefinition(id, data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.definitions.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.definitions.detail(variables.id) });
      toast.success(i18n.t('workflow.definitionUpdated', 'Workflow definition updated'));
    },
    onError: handleMutationError,
  });
}
