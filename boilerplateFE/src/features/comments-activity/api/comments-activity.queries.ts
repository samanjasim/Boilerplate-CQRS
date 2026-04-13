import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { commentsActivityApi } from './comments-activity.api';
import type { CreateCommentData, EditCommentData } from '@/types/comments-activity.types';
import { toast } from 'sonner';
import i18n from '@/i18n';

// ── Queries ────────────────────────────────────────────────────────────────

export function useTimeline(
  entityType: string,
  entityId: string,
  params?: { filter?: string; pageNumber?: number; pageSize?: number },
) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.timeline.list(entityType, entityId, params),
    queryFn: () => commentsActivityApi.getTimeline({ entityType, entityId, ...params }),
    enabled: !!entityType && !!entityId,
  });
}

export function useComments(
  entityType: string,
  entityId: string,
  params?: { pageNumber?: number; pageSize?: number },
) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.comments.list(entityType, entityId, params),
    queryFn: () => commentsActivityApi.getComments({ entityType, entityId, ...params }),
    enabled: !!entityType && !!entityId,
  });
}

export function useWatchStatus(entityType: string, entityId: string) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.watchers.status(entityType, entityId),
    queryFn: () => commentsActivityApi.getWatchStatus({ entityType, entityId }),
    enabled: !!entityType && !!entityId,
  });
}

export function useMentionableUsers(search?: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.mentionableUsers(search),
    queryFn: () => commentsActivityApi.getMentionableUsers({ search, pageSize: 10 }),
    enabled: enabled,
  });
}

// ── Mutations ──────────────────────────────────────────────────────────────

export function useAddComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateCommentData) => commentsActivityApi.addComment(data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      queryClient.invalidateQueries({
        queryKey: queryKeys.commentsActivity.watchers.status(variables.entityType, variables.entityId),
      });
      toast.success(i18n.t('commentsActivity.commentAdded', 'Comment added'));
    },
  });
}

export function useEditComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: EditCommentData) => commentsActivityApi.editComment(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      toast.success(i18n.t('commentsActivity.commentEdited', 'Comment updated'));
    },
  });
}

export function useDeleteComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => commentsActivityApi.deleteComment(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      toast.success(i18n.t('commentsActivity.commentDeleted', 'Comment deleted'));
    },
  });
}

export function useToggleReaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ commentId, reactionType }: { commentId: string; reactionType: string }) =>
      commentsActivityApi.toggleReaction(commentId, { reactionType }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
    },
  });
}

export function useWatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { entityType: string; entityId: string }) => commentsActivityApi.watch(data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.commentsActivity.watchers.status(variables.entityType, variables.entityId),
      });
      toast.success(i18n.t('commentsActivity.watching', 'You are now watching this item'));
    },
  });
}

export function useUnwatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { entityType: string; entityId: string }) => commentsActivityApi.unwatch(data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.commentsActivity.watchers.status(variables.entityType, variables.entityId),
      });
      toast.success(i18n.t('commentsActivity.unwatched', 'You stopped watching this item'));
    },
  });
}
