import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type {
  CreateCommentData,
  EditCommentData,
  ToggleReactionData,
} from '@/types/comments-activity.types';

export const commentsActivityApi = {
  getTimeline: (params: { entityType: string; entityId: string; filter?: string; pageNumber?: number; pageSize?: number }) =>
    apiClient.get(API_ENDPOINTS.COMMENTS_ACTIVITY.TIMELINE, { params }).then((r) => r.data),

  getComments: (params: { entityType: string; entityId: string; pageNumber?: number; pageSize?: number }) =>
    apiClient.get(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENTS, { params }).then((r) => r.data),

  addComment: (data: CreateCommentData) =>
    apiClient.post(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENTS, data).then((r) => r.data),

  editComment: (data: EditCommentData) =>
    apiClient.put(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_DETAIL(data.id), data).then((r) => r.data),

  deleteComment: (id: string) =>
    apiClient.delete(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_DETAIL(id)).then((r) => r.data),

  toggleReaction: (commentId: string, data: ToggleReactionData) =>
    apiClient.post(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_REACTIONS(commentId), data).then((r) => r.data),

  removeReaction: (commentId: string, reactionType: string) =>
    apiClient.delete(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_REACTION(commentId, reactionType)).then((r) => r.data),

  getWatchStatus: (params: { entityType: string; entityId: string }) =>
    apiClient.get(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS_STATUS, { params }).then((r) => r.data),

  watch: (data: { entityType: string; entityId: string }) =>
    apiClient.post(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS, data).then((r) => r.data),

  unwatch: (params: { entityType: string; entityId: string }) =>
    apiClient.delete(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS, { params }).then((r) => r.data),

  getMentionableUsers: (params: {
    search?: string;
    pageSize?: number;
    entityType?: string;
    entityId?: string;
  }) =>
    apiClient.get(API_ENDPOINTS.COMMENTS_ACTIVITY.MENTIONABLE_USERS, { params }).then((r) => r.data),
};
