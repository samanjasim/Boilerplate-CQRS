import { api } from '@/lib/api';
import { API_ENDPOINTS } from '@/config';
import type {
  Comment,
  CreateCommentData,
  EditCommentData,
  MentionableUser,
  ReactionSummary,
  TimelineItem,
  ToggleReactionData,
  WatchStatus,
} from '@/types/comments-activity.types';
import type { PagedResult } from '@/types';

interface TimelineParams {
  entityType: string;
  entityId: string;
  filter?: string;
  pageNumber?: number;
  pageSize?: number;
}

interface CommentsParams {
  entityType: string;
  entityId: string;
  pageNumber?: number;
  pageSize?: number;
}

interface MentionableUsersParams {
  search?: string;
  pageSize?: number;
  entityType?: string;
  entityId?: string;
}

interface WatchTargetParams {
  entityType: string;
  entityId: string;
}

export const commentsActivityApi = {
  getTimeline: (params: TimelineParams): Promise<PagedResult<TimelineItem>> =>
    api.paged<TimelineItem>(API_ENDPOINTS.COMMENTS_ACTIVITY.TIMELINE, params),

  getComments: (params: CommentsParams): Promise<PagedResult<Comment>> =>
    api.paged<Comment>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENTS, params),

  addComment: (data: CreateCommentData): Promise<Comment> =>
    api.post<Comment>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENTS, data),

  editComment: (data: EditCommentData): Promise<Comment> =>
    api.put<Comment>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_DETAIL(data.id), data),

  deleteComment: (id: string): Promise<void> =>
    api.delete<void>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_DETAIL(id)),

  toggleReaction: (commentId: string, data: ToggleReactionData): Promise<ReactionSummary[]> =>
    api.post<ReactionSummary[]>(
      API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_REACTIONS(commentId),
      data,
    ),

  removeReaction: (commentId: string, reactionType: string): Promise<ReactionSummary[]> =>
    api.delete<ReactionSummary[]>(
      API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_REACTION(commentId, reactionType),
    ),

  getWatchStatus: (params: WatchTargetParams): Promise<WatchStatus> =>
    api.get<WatchStatus>(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS_STATUS, params),

  watch: (data: WatchTargetParams): Promise<void> =>
    api.post<void>(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS, data),

  // BE reads from query string on DELETE; api.delete doesn't accept params,
  // so build the URL inline. One callsite — not worth widening api.delete's
  // signature.
  unwatch: (params: WatchTargetParams): Promise<void> => {
    const qs = new URLSearchParams({
      entityType: params.entityType,
      entityId: params.entityId,
    }).toString();
    return api.delete<void>(`${API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS}?${qs}`);
  },

  getMentionableUsers: (params: MentionableUsersParams): Promise<PagedResult<MentionableUser>> =>
    api.paged<MentionableUser>(API_ENDPOINTS.COMMENTS_ACTIVITY.MENTIONABLE_USERS, params),
};
