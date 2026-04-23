import { useState } from 'react';
import { CommentItem } from './CommentItem';
import { CommentComposer } from './CommentComposer';
import type { Comment } from '@/types/comments-activity.types';

interface CommentThreadProps {
  comment: Comment;
}

export function CommentThread({ comment }: CommentThreadProps) {
  const [replyingTo, setReplyingTo] = useState<string | null>(null);
  const [editingComment, setEditingComment] = useState<Comment | null>(null);

  const handleReply = (commentId: string) => {
    setReplyingTo(commentId);
    setEditingComment(null);
  };

  const handleEditStart = (c: Comment) => {
    setEditingComment(c);
    setReplyingTo(null);
  };

  const handleCancelReply = () => {
    setReplyingTo(null);
  };

  const handleCancelEdit = () => {
    setEditingComment(null);
  };

  return (
    <div>
      {/* Top-level comment — or edit mode if editing the top-level */}
      {editingComment?.id === comment.id ? (
        <div className="py-3">
          <CommentComposer
            entityType={comment.entityType}
            entityId={comment.entityId}
            editMode={{ commentId: comment.id, initialBody: comment.body }}
            onCancel={handleCancelEdit}
          />
        </div>
      ) : (
        <CommentItem
          comment={comment}
          onReply={handleReply}
          onEditStart={handleEditStart}
        />
      )}

      {/* Inline reply composer */}
      {replyingTo === comment.id && (
        <div className="ltr:ml-8 ltr:pl-4 rtl:mr-8 rtl:pr-4 pb-2">
          <CommentComposer
            entityType={comment.entityType}
            entityId={comment.entityId}
            parentCommentId={comment.id}
            onCancel={handleCancelReply}
          />
        </div>
      )}

      {/* Replies */}
      {comment.replies?.map((reply) =>
        editingComment?.id === reply.id ? (
          <div key={reply.id} className="ltr:ml-8 ltr:border-l-2 ltr:pl-4 rtl:mr-8 rtl:border-r-2 rtl:pr-4 border-border/30 py-3">
            <CommentComposer
              entityType={reply.entityType}
              entityId={reply.entityId}
              editMode={{ commentId: reply.id, initialBody: reply.body }}
              onCancel={handleCancelEdit}
            />
          </div>
        ) : (
          <CommentItem
            key={reply.id}
            comment={reply}
            isReply
            onEditStart={handleEditStart}
          />
        ),
      )}
    </div>
  );
}
