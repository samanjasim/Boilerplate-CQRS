import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MoreHorizontal, Reply, Pencil, Trash2, Paperclip } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
} from '@/components/ui/dropdown-menu';
import { ConfirmDialog } from '@/components/common';
import { UserAvatar } from '@/components/common/UserAvatar';
import { useAuthStore, selectUser } from '@/stores';
import { usePermissions, useTimeAgo } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { useDeleteComment } from '../api';
import { ReactionPicker } from './ReactionPicker';
import { cn } from '@/lib/utils';
import type { Comment } from '@/types/comments-activity.types';

interface CommentItemProps {
  comment: Comment;
  isReply?: boolean;
  onReply?: (commentId: string) => void;
  onEditStart?: (comment: Comment) => void;
}

export function CommentItem({ comment, isReply, onReply, onEditStart }: CommentItemProps) {
  const { t } = useTranslation();
  const currentUser = useAuthStore(selectUser);
  const { hasPermission } = usePermissions();
  const { mutate: deleteComment, isPending: isDeleting } = useDeleteComment();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  const timeAgo = useTimeAgo(comment.createdAt);

  const isAuthor = currentUser?.id === comment.authorId;
  const canManage = hasPermission(PERMISSIONS.Comments.Manage);
  const canEdit = isAuthor || canManage;
  const canDelete = isAuthor || canManage;
  const showActions = canEdit || canDelete || (!isReply && onReply);

  // Split author name into first/last for UserAvatar
  const nameParts = comment.authorName.split(' ');
  const firstName = nameParts[0];
  const lastName = nameParts.length > 1 ? nameParts[nameParts.length - 1] : undefined;

  if (comment.isDeleted) {
    return (
      <div
        className={cn(
          'flex items-start gap-3 py-3',
          isReply && 'ltr:ml-8 ltr:border-l-2 ltr:pl-4 rtl:mr-8 rtl:border-r-2 rtl:pr-4 border-border/30',
        )}
      >
        <UserAvatar firstName="?" size="sm" className="opacity-40" />
        <p className="text-sm italic text-muted-foreground">
          {t('commentsActivity.deletedComment', '[This comment has been deleted]')}
        </p>
      </div>
    );
  }

  const handleDelete = () => {
    deleteComment(comment.id);
    setShowDeleteDialog(false);
  };

  return (
    <div
      className={cn(
        'group py-3',
        isReply && 'ltr:ml-8 ltr:border-l-2 ltr:pl-4 rtl:mr-8 rtl:border-r-2 rtl:pr-4 border-border/30',
      )}
    >
      <div className="flex items-start gap-3">
        <UserAvatar firstName={firstName} lastName={lastName} size="sm" />
        <div className="min-w-0 flex-1">
          {/* Header: author + time + edited badge */}
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-foreground">{comment.authorName}</span>
            <span className="text-xs text-muted-foreground">{timeAgo}</span>
            {comment.modifiedAt && (
              <Badge variant="secondary" className="text-[10px]">
                {t('commentsActivity.edited', 'edited')}
              </Badge>
            )}

            {/* Actions menu (visible on hover) */}
            {showActions && (
              <div className="ltr:ml-auto rtl:mr-auto opacity-0 transition-opacity duration-150 group-hover:opacity-100">
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7"
                      aria-label={t('common.actions')}
                    >
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    {!isReply && onReply && (
                      <DropdownMenuItem onClick={() => onReply(comment.id)}>
                        <Reply className="ltr:mr-2 rtl:ml-2 h-4 w-4" />
                        {t('commentsActivity.reply', 'Reply')}
                      </DropdownMenuItem>
                    )}
                    {canEdit && onEditStart && (
                      <DropdownMenuItem onClick={() => onEditStart(comment)}>
                        <Pencil className="ltr:mr-2 rtl:ml-2 h-4 w-4" />
                        {t('commentsActivity.edit', 'Edit')}
                      </DropdownMenuItem>
                    )}
                    {canDelete && (
                      <>
                        {((!isReply && onReply) || (canEdit && onEditStart)) && <DropdownMenuSeparator />}
                        <DropdownMenuItem
                          onClick={() => setShowDeleteDialog(true)}
                          disabled={isDeleting}
                          className="text-destructive focus:text-destructive"
                        >
                          <Trash2 className="ltr:mr-2 rtl:ml-2 h-4 w-4" />
                          {t('commentsActivity.delete', 'Delete')}
                        </DropdownMenuItem>
                      </>
                    )}
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            )}
          </div>

          {/* Body (markdown) — explicit utilities instead of `prose` so theme tokens drive color */}
          <div className="mt-1 text-sm leading-6 text-foreground [&_p]:my-1 [&_a]:text-primary [&_a]:underline [&_code]:rounded [&_code]:bg-muted [&_code]:px-1">
            <ReactMarkdown
              components={{
                a: ({ href, children }) => {
                  const isUuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(href ?? '');
                  if (isUuid) {
                    return (
                      <span className="inline-flex items-center rounded bg-primary/10 px-1 py-0.5 text-xs font-medium text-primary no-underline">
                        @{children}
                      </span>
                    );
                  }
                  return (
                    <a href={href} target="_blank" rel="noopener noreferrer">
                      {children}
                    </a>
                  );
                },
              }}
            >
              {/* Wire format is `@[Name](id)`; the link renderer prepends its own `@`,
                  so strip the literal one to avoid `@@Name`. */}
              {comment.body.replace(/@(\[[^\]]+\]\([0-9a-f-]{36}\))/gi, '$1')}
            </ReactMarkdown>
          </div>

          {/* Attachments */}
          {comment.attachments.length > 0 && (
            <div className="mt-2 flex flex-wrap gap-2">
              {comment.attachments.map((a) => (
                <a
                  key={a.id}
                  href={a.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1.5 rounded-lg bg-secondary px-2.5 py-1 text-xs text-muted-foreground transition-colors duration-150 hover:text-foreground"
                >
                  <Paperclip className="h-3 w-3" />
                  <span className="max-w-[120px] truncate">{a.fileName}</span>
                  <span className="text-muted-foreground/60">
                    ({(a.size / 1024).toFixed(0)}KB)
                  </span>
                </a>
              ))}
            </div>
          )}

          {/* Reactions */}
          {comment.reactions.length > 0 && (
            <div className="mt-2">
              <ReactionPicker commentId={comment.id} reactions={comment.reactions} />
            </div>
          )}
          {comment.reactions.length === 0 && (
            <div className="mt-2 opacity-0 transition-opacity duration-150 group-hover:opacity-100">
              <ReactionPicker commentId={comment.id} reactions={[]} />
            </div>
          )}
        </div>
      </div>

      <ConfirmDialog
        isOpen={showDeleteDialog}
        onClose={() => setShowDeleteDialog(false)}
        title={t('commentsActivity.deleteTitle', 'Delete Comment')}
        description={t(
          'commentsActivity.deleteDescription',
          'Are you sure you want to delete this comment? This action cannot be undone.',
        )}
        onConfirm={handleDelete}
        confirmLabel={t('commentsActivity.delete', 'Delete')}
        variant="danger"
      />
    </div>
  );
}
