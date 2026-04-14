import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { queryKeys } from '@/lib/query/keys';
import { commentsActivityApi } from './comments-activity.api';
import type {
  Comment,
  CreateCommentData,
  EditCommentData,
  ReactionSummary,
  TimelineItem,
} from '@/types/comments-activity.types';
import { toast } from 'sonner';
import i18n from '@/i18n';

type PagedResponse<T> = { data: T[]; [key: string]: unknown };

function toggleReactionOnList(comments: Comment[] | undefined, commentId: string, reactionType: string): Comment[] | undefined {
  if (!comments) return comments;
  return comments.map((comment) => {
    const updated = toggleReactionOnComment(comment, commentId, reactionType);
    if (updated !== comment) return updated;
    if (comment.replies && comment.replies.length > 0) {
      const replies = toggleReactionOnList(comment.replies, commentId, reactionType);
      if (replies !== comment.replies) return { ...comment, replies };
    }
    return comment;
  });
}

function toggleReactionOnComment(comment: Comment, commentId: string, reactionType: string): Comment {
  if (comment.id !== commentId) return comment;
  const existing = comment.reactions.find((r) => r.reactionType === reactionType);
  let reactions: ReactionSummary[];
  if (existing) {
    if (existing.userReacted) {
      const nextCount = Math.max(0, existing.count - 1);
      reactions = nextCount === 0
        ? comment.reactions.filter((r) => r.reactionType !== reactionType)
        : comment.reactions.map((r) =>
            r.reactionType === reactionType ? { ...r, count: nextCount, userReacted: false } : r,
          );
    } else {
      reactions = comment.reactions.map((r) =>
        r.reactionType === reactionType ? { ...r, count: r.count + 1, userReacted: true } : r,
      );
    }
  } else {
    reactions = [...comment.reactions, { reactionType, count: 1, userReacted: true }];
  }
  return { ...comment, reactions };
}

function handleMutationError(error: unknown) {
  const message =
    error instanceof AxiosError && error.response?.data?.message
      ? error.response.data.message
      : i18n.t('common.error', 'Something went wrong');
  toast.error(message);
}

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

export function useMentionableUsers(
  search?: string,
  enabled = true,
  entityType?: string,
  entityId?: string,
) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.mentionableUsers(search, entityType, entityId),
    queryFn: () =>
      commentsActivityApi.getMentionableUsers({ search, pageSize: 10, entityType, entityId }),
    enabled,
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
    onError: handleMutationError,
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
    onError: handleMutationError,
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
    onError: handleMutationError,
  });
}

export function useToggleReaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ commentId, reactionType }: { commentId: string; reactionType: string }) =>
      commentsActivityApi.toggleReaction(commentId, { reactionType }),
    // Optimistic update: mutate every cached comments and timeline page so the
    // reaction badge flips instantly. Snapshot for rollback on error.
    onMutate: async ({ commentId, reactionType }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      await queryClient.cancelQueries({ queryKey: queryKeys.commentsActivity.timeline.all });

      const commentSnapshots = queryClient.getQueriesData<PagedResponse<Comment>>({
        queryKey: queryKeys.commentsActivity.comments.all,
      });
      const timelineSnapshots = queryClient.getQueriesData<PagedResponse<TimelineItem>>({
        queryKey: queryKeys.commentsActivity.timeline.all,
      });

      queryClient.setQueriesData<PagedResponse<Comment>>(
        { queryKey: queryKeys.commentsActivity.comments.all },
        (old) => {
          if (!old?.data) return old;
          const next = toggleReactionOnList(old.data, commentId, reactionType);
          return next === old.data ? old : { ...old, data: next ?? [] };
        },
      );

      queryClient.setQueriesData<PagedResponse<TimelineItem>>(
        { queryKey: queryKeys.commentsActivity.timeline.all },
        (old) => {
          if (!old?.data) return old;
          let changed = false;
          const next = old.data.map((item) => {
            if (item.type !== 'comment' || !item.comment) return item;
            const updated = toggleReactionOnComment(item.comment, commentId, reactionType);
            if (updated === item.comment) return item;
            changed = true;
            return { ...item, comment: updated };
          });
          return changed ? { ...old, data: next } : old;
        },
      );

      return { commentSnapshots, timelineSnapshots };
    },
    onError: (error, _vars, context) => {
      context?.commentSnapshots.forEach(([key, value]) => queryClient.setQueryData(key, value));
      context?.timelineSnapshots.forEach(([key, value]) => queryClient.setQueryData(key, value));
      handleMutationError(error);
    },
    onSettled: () => {
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
    onError: handleMutationError,
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
    onError: handleMutationError,
  });
}
