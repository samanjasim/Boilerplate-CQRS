export interface CommentAttachment {
  id: string;
  fileMetadataId: string;
  fileName: string;
  contentType: string;
  size: number;
  url?: string;
}

export interface MentionRef {
  userId: string;
  username: string;
  displayName: string;
}

export interface ReactionSummary {
  reactionType: string;
  count: number;
  userReacted: boolean;
}

export interface Comment {
  id: string;
  entityType: string;
  entityId: string;
  parentCommentId?: string;
  authorId: string;
  authorName: string;
  authorEmail: string;
  body: string;
  mentions?: MentionRef[];
  attachments: CommentAttachment[];
  reactions: ReactionSummary[];
  isDeleted: boolean;
  replies?: Comment[];
  createdAt: string;
  modifiedAt?: string;
}

export interface ActivityEntry {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  actorId?: string;
  actorName?: string;
  metadataJson?: string;
  description?: string;
  createdAt: string;
}

export interface TimelineItem {
  type: 'comment' | 'activity';
  comment?: Comment;
  activity?: ActivityEntry;
  timestamp: string;
}

export interface WatchStatus {
  isWatching: boolean;
  watcherCount: number;
}

export interface MentionableUser {
  id: string;
  username: string;
  displayName: string;
  email: string;
}

// Request types
export interface CreateCommentData {
  entityType: string;
  entityId: string;
  body: string;
  mentionUserIds?: string[];
  parentCommentId?: string;
  attachmentFileIds?: string[];
}

export interface EditCommentData {
  id: string;
  body: string;
  mentionUserIds?: string[];
}

export interface ToggleReactionData {
  reactionType: string;
}
