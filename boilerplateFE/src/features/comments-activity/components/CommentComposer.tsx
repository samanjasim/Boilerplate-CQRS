import { useState, useRef, useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Send, Paperclip, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { useAddComment, useEditComment } from '../api';
import { useUploadFile } from '@/features/files/api';
import { getCaretCoordinates } from '@/lib/dom/getCaretCoordinates';
import { MentionAutocomplete, type MentionAutocompleteHandle } from './MentionAutocomplete';
import type { MentionableUser } from '@/types/comments-activity.types';

const MENTION_POPUP_WIDTH = 288;
const MENTION_POPUP_GAP = 6;
// Conservative estimate of fully-populated popup height (header + ~5 rows + footer).
// Used only for placement decisions, not actual sizing.
const MENTION_POPUP_HEIGHT = 320;

type MentionRef = { id: string; displayName: string };

// Convert stored markdown form `@[Name](id)` → display form `@Name` and pull out mentions.
function deserializeMentions(stored: string): { displayBody: string; mentions: MentionRef[] } {
  const mentions: MentionRef[] = [];
  const displayBody = stored.replace(/@\[([^\]]+)\]\(([^)]+)\)/g, (_m, displayName: string, id: string) => {
    mentions.push({ id, displayName });
    return `@${displayName}`;
  });
  return { displayBody, mentions };
}

// Convert display form `@Name` → stored markdown `@[Name](id)` using tracked mentions.
// Walks `mentions` in insertion order so duplicate display-names map to the right ids.
function serializeMentions(displayBody: string, mentions: MentionRef[]): string {
  let result = displayBody;
  let cursor = 0;
  for (const m of mentions) {
    const needle = `@${m.displayName}`;
    const idx = result.indexOf(needle, cursor);
    if (idx < 0) continue;
    const replacement = `@[${m.displayName}](${m.id})`;
    result = result.slice(0, idx) + replacement + result.slice(idx + needle.length);
    cursor = idx + replacement.length;
  }
  return result;
}

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
  const [body, setBody] = useState(() =>
    editMode?.initialBody ? deserializeMentions(editMode.initialBody).displayBody : '',
  );
  const [mentions, setMentions] = useState<MentionRef[]>(() =>
    editMode?.initialBody ? deserializeMentions(editMode.initialBody).mentions : [],
  );
  const [mentionSearch, setMentionSearch] = useState('');
  const [mentionVisible, setMentionVisible] = useState(false);
  const [mentionPos, setMentionPos] = useState<
    { top: number; left: number; placement: 'top' | 'bottom' } | undefined
  >();
  const [attachments, setAttachments] = useState<{ fileId: string; fileName: string }[]>([]);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const mentionStartRef = useRef<number>(-1);
  const mentionRef = useRef<MentionAutocompleteHandle>(null);

  const { mutate: addComment, isPending: isAdding } = useAddComment();
  const { mutate: editComment, isPending: isEditing } = useEditComment();
  const { mutateAsync: uploadFile, isPending: isUploading } = useUploadFile();
  const isPending = isAdding || isEditing;

  // Re-seed body/mentions when switching which comment is being edited.
  // Adjust-state-in-render pattern — avoids the cascading rerender of an
  // effect-based reset and keeps the next paint in sync with the new prop.
  const [lastEditCommentId, setLastEditCommentId] = useState(editMode?.commentId);
  if (editMode?.commentId !== lastEditCommentId) {
    setLastEditCommentId(editMode?.commentId);
    if (editMode?.initialBody) {
      const { displayBody, mentions: parsed } = deserializeMentions(editMode.initialBody);
      setBody(displayBody);
      setMentions(parsed);
    }
  }

  // Derived: drop tracked mentions whose display text was backspaced out of
  // the body so notifications stay honest.
  const activeMentions = useMemo(
    () => mentions.filter((m) => body.includes(`@${m.displayName}`)),
    [mentions, body],
  );

  const mentionUserIds = useMemo(
    () => Array.from(new Set(activeMentions.map((m) => m.id))),
    [activeMentions],
  );

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

          // Anchor the autocomplete to the caret (Slack/Teams-style).
          // direction is mirrored inside getCaretCoordinates so RTL works automatically.
          const ta = textareaRef.current;
          if (ta) {
            const caret = getCaretCoordinates(ta, lastAtIndex);
            const containerWidth = ta.offsetWidth;
            const isRtl = getComputedStyle(ta).direction === 'rtl';

            // Horizontal: anchor at caret X, clamp so popup stays in-bounds.
            let left = caret.left - ta.scrollLeft;
            if (isRtl) left = left - MENTION_POPUP_WIDTH;
            left = Math.max(0, Math.min(left, containerWidth - MENTION_POPUP_WIDTH));

            // Vertical: prefer below, flip to above only when below can't fit
            // the full popup AND above has strictly more room. CSS handles the
            // upward shift with translateY(-100%) so we don't need to know the
            // popup's actual height.
            const taRect = ta.getBoundingClientRect();
            const caretViewportTop = taRect.top + caret.top - ta.scrollTop;
            const caretViewportBottom = caretViewportTop + caret.height;
            const spaceBelow = window.innerHeight - caretViewportBottom;
            const spaceAbove = caretViewportTop;
            const placeAbove = spaceBelow < MENTION_POPUP_HEIGHT && spaceAbove > spaceBelow;

            const top = placeAbove
              ? caret.top - ta.scrollTop - MENTION_POPUP_GAP
              : caret.top - ta.scrollTop + caret.height + MENTION_POPUP_GAP;

            setMentionPos({ top, left, placement: placeAbove ? 'top' : 'bottom' });
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
      // Insert plain `@Name ` for a clean, chat-like composer.
      // The `@[Name](id)` markdown is reconstructed at submit time.
      const mention = `@${user.displayName} `;

      setBody(before + mention + after);
      setMentions((prev) => [...prev, { id: user.id, displayName: user.displayName }]);
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

  const handleFileSelect = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;
      try {
        const result = await uploadFile({ file, category: 'Attachment' });
        setAttachments((prev) => [...prev, { fileId: result.id, fileName: result.fileName }]);
      } catch {
        // Upload failed — onError in mutation hook will handle toast
      }
      // Reset input so same file can be re-selected
      if (fileInputRef.current) fileInputRef.current.value = '';
    },
    [uploadFile],
  );

  const removeAttachment = useCallback((fileId: string) => {
    setAttachments((prev) => prev.filter((a) => a.fileId !== fileId));
  }, []);

  const handleSubmit = () => {
    const trimmed = body.trim();
    if (!trimmed) return;

    const serialized = serializeMentions(trimmed, activeMentions);

    if (editMode) {
      editComment(
        { id: editMode.commentId, body: serialized, mentionUserIds },
        {
          onSuccess: () => {
            setBody('');
            setMentions([]);
            setAttachments([]);
            onCancel?.();
          },
        },
      );
    } else {
      addComment(
        {
          entityType,
          entityId,
          body: serialized,
          mentionUserIds: mentionUserIds.length > 0 ? mentionUserIds : undefined,
          parentCommentId,
          attachmentFileIds: attachments.length > 0 ? attachments.map((a) => a.fileId) : undefined,
        },
        {
          onSuccess: () => {
            setBody('');
            setMentions([]);
            setAttachments([]);
            if (onCancel) {
              onCancel();
            } else {
              // Root composer: refocus so the user can keep typing follow-ups
              // without reaching for the mouse.
              requestAnimationFrame(() => textareaRef.current?.focus());
            }
          },
        },
      );
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (mentionVisible) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        mentionRef.current?.moveDown();
        return;
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault();
        mentionRef.current?.moveUp();
        return;
      }
      if (e.key === 'Enter' || e.key === 'Tab') {
        if (mentionRef.current?.selectActive()) {
          e.preventDefault();
          return;
        }
      }
      if (e.key === 'Escape') {
        e.preventDefault();
        setMentionVisible(false);
        return;
      }
    }

    // Submit on Cmd/Ctrl + Enter
    if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
      e.preventDefault();
      handleSubmit();
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
          ref={mentionRef}
          search={mentionSearch}
          onSelect={handleMentionSelect}
          onClose={() => setMentionVisible(false)}
          visible={mentionVisible}
          entityType={entityType}
          entityId={entityId}
          position={mentionPos}
        />
      </div>

      {/* Attached files */}
      {attachments.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {attachments.map((a) => (
            <span
              key={a.fileId}
              className="inline-flex items-center gap-1 rounded-md bg-secondary px-2 py-0.5 text-xs text-muted-foreground"
            >
              <Paperclip className="h-3 w-3" />
              <span className="max-w-[120px] truncate">{a.fileName}</span>
              <button
                type="button"
                onClick={() => removeAttachment(a.fileId)}
                className="hover:text-foreground"
              >
                <X className="h-3 w-3" />
              </button>
            </span>
          ))}
        </div>
      )}

      <div className="flex items-center justify-between">
        <div>
          {!editMode && (
            <>
              <input
                ref={fileInputRef}
                type="file"
                className="hidden"
                onChange={handleFileSelect}
              />
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => fileInputRef.current?.click()}
                disabled={isPending || isUploading}
              >
                <Paperclip className="ltr:mr-1.5 rtl:ml-1.5 h-3.5 w-3.5" />
                {isUploading
                  ? t('common.uploading', 'Uploading...')
                  : t('commentsActivity.attach', 'Attach')}
              </Button>
            </>
          )}
        </div>
        <div className="flex items-center gap-2">
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
    </div>
  );
}
