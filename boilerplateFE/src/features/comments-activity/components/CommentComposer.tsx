import { useState, useRef, useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { useAddComment, useEditComment } from '../api';
import { MentionAutocomplete } from './MentionAutocomplete';
import type { MentionableUser } from '@/types/comments-activity.types';

interface CommentComposerProps {
  entityType: string;
  entityId: string;
  parentCommentId?: string;
  onCancel?: () => void;
  editMode?: { commentId: string; initialBody: string };
}

export function CommentComposer({
  entityType,
  entityId,
  parentCommentId,
  onCancel,
  editMode,
}: CommentComposerProps) {
  const { t } = useTranslation();
  const [body, setBody] = useState(editMode?.initialBody ?? '');
  const [mentionUserIds, setMentionUserIds] = useState<string[]>([]);
  const [mentionSearch, setMentionSearch] = useState('');
  const [mentionVisible, setMentionVisible] = useState(false);
  const [mentionPos, setMentionPos] = useState<{ top: number; left: number } | undefined>();
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const mentionStartRef = useRef<number>(-1);

  const { mutate: addComment, isPending: isAdding } = useAddComment();
  const { mutate: editComment, isPending: isEditing } = useEditComment();
  const isPending = isAdding || isEditing;

  useEffect(() => {
    if (editMode?.initialBody) {
      setBody(editMode.initialBody);
    }
  }, [editMode?.initialBody]);

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      const value = e.target.value;
      setBody(value);

      const cursorPos = e.target.selectionStart;
      // Look back from cursor to find an @ that starts a mention
      const textBeforeCursor = value.slice(0, cursorPos);
      const lastAtIndex = textBeforeCursor.lastIndexOf('@');

      if (lastAtIndex >= 0) {
        const charBefore = lastAtIndex > 0 ? value[lastAtIndex - 1] : ' ';
        const searchStr = textBeforeCursor.slice(lastAtIndex + 1);
        // Only trigger if @ is at start or after whitespace, and no space in search
        if ((charBefore === ' ' || charBefore === '\n' || lastAtIndex === 0) && !searchStr.includes(' ')) {
          mentionStartRef.current = lastAtIndex;
          setMentionSearch(searchStr);
          setMentionVisible(true);

          // Position the autocomplete below the textarea
          if (textareaRef.current) {
            const rect = textareaRef.current.getBoundingClientRect();
            setMentionPos({ top: rect.height + 4, left: 0 });
          }
          return;
        }
      }

      setMentionVisible(false);
      setMentionSearch('');
    },
    [],
  );

  const handleMentionSelect = useCallback(
    (user: MentionableUser) => {
      const start = mentionStartRef.current;
      if (start < 0) return;

      const before = body.slice(0, start);
      const cursorPos = textareaRef.current?.selectionStart ?? body.length;
      const after = body.slice(cursorPos);
      const mention = `@[${user.displayName}](${user.id}) `;

      setBody(before + mention + after);
      setMentionUserIds((prev) => (prev.includes(user.id) ? prev : [...prev, user.id]));
      setMentionVisible(false);
      setMentionSearch('');
      mentionStartRef.current = -1;

      // Refocus textarea
      requestAnimationFrame(() => {
        if (textareaRef.current) {
          const newPos = (before + mention).length;
          textareaRef.current.focus();
          textareaRef.current.setSelectionRange(newPos, newPos);
        }
      });
    },
    [body],
  );

  const handleSubmit = () => {
    const trimmed = body.trim();
    if (!trimmed) return;

    if (editMode) {
      editComment(
        { id: editMode.commentId, body: trimmed, mentionUserIds },
        {
          onSuccess: () => {
            setBody('');
            setMentionUserIds([]);
            onCancel?.();
          },
        },
      );
    } else {
      addComment(
        {
          entityType,
          entityId,
          body: trimmed,
          mentionUserIds: mentionUserIds.length > 0 ? mentionUserIds : undefined,
          parentCommentId,
        },
        {
          onSuccess: () => {
            setBody('');
            setMentionUserIds([]);
            onCancel?.();
          },
        },
      );
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    // Submit on Cmd/Ctrl + Enter
    if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
      e.preventDefault();
      handleSubmit();
    }
    // Close mention on Escape
    if (e.key === 'Escape' && mentionVisible) {
      setMentionVisible(false);
    }
  };

  const showCancelButton = !!parentCommentId || !!editMode;

  return (
    <div className="space-y-2">
      {parentCommentId && !editMode && (
        <p className="text-xs font-medium text-muted-foreground">
          {t('commentsActivity.replyingTo', 'Replying to comment...')}
        </p>
      )}
      {editMode && (
        <p className="text-xs font-medium text-muted-foreground">
          {t('commentsActivity.editing', 'Editing comment...')}
        </p>
      )}

      <div className="relative">
        <Textarea
          ref={textareaRef}
          value={body}
          onChange={handleChange}
          onKeyDown={handleKeyDown}
          placeholder={t('commentsActivity.writeComment', 'Write a comment...')}
          className="min-h-[80px] resize-y"
          disabled={isPending}
        />
        <MentionAutocomplete
          search={mentionSearch}
          onSelect={handleMentionSelect}
          visible={mentionVisible}
          position={mentionPos}
        />
      </div>

      <div className="flex items-center justify-end gap-2">
        {showCancelButton && (
          <Button variant="ghost" size="sm" onClick={onCancel} disabled={isPending}>
            {t('common.cancel', 'Cancel')}
          </Button>
        )}
        <Button
          size="sm"
          onClick={handleSubmit}
          disabled={!body.trim() || isPending}
        >
          <Send className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />
          {editMode
            ? t('commentsActivity.save', 'Save')
            : t('commentsActivity.send', 'Send')}
        </Button>
      </div>
    </div>
  );
}
