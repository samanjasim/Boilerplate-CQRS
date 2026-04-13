import { useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover';
import { useToggleReaction } from '../api';
import { cn } from '@/lib/utils';
import type { ReactionSummary } from '@/types/comments-activity.types';

const PRESET_EMOJIS = [
  { emoji: '\uD83D\uDC4D', type: 'thumbs_up' },
  { emoji: '\uD83D\uDC4E', type: 'thumbs_down' },
  { emoji: '\u2764\uFE0F', type: 'heart' },
  { emoji: '\uD83D\uDE80', type: 'rocket' },
  { emoji: '\uD83D\uDC40', type: 'eyes' },
  { emoji: '\uD83C\uDF89', type: 'tada' },
];

function emojiForType(type: string): string {
  return PRESET_EMOJIS.find((e) => e.type === type)?.emoji ?? type;
}

interface ReactionPickerProps {
  commentId: string;
  reactions: ReactionSummary[];
}

export function ReactionPicker({ commentId, reactions }: ReactionPickerProps) {
  const [open, setOpen] = useState(false);
  const { mutate: toggleReaction } = useToggleReaction();

  const handleToggle = (reactionType: string) => {
    toggleReaction({ commentId, reactionType });
    setOpen(false);
  };

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {reactions.map((r) => (
        <button
          key={r.reactionType}
          type="button"
          onClick={() => handleToggle(r.reactionType)}
          className={cn(
            'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors duration-150',
            r.userReacted
              ? 'border-primary/30 [background:var(--active-bg)] [color:var(--active-text)]'
              : 'border-border/50 bg-secondary text-muted-foreground hover:bg-secondary/70',
          )}
        >
          <span>{emojiForType(r.reactionType)}</span>
          <span>{r.count}</span>
        </button>
      ))}

      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7 rounded-full"
          >
            <Plus className="h-3.5 w-3.5" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-2" align="start">
          <div className="grid grid-cols-6 gap-1">
            {PRESET_EMOJIS.map((e) => (
              <button
                key={e.type}
                type="button"
                onClick={() => handleToggle(e.type)}
                className="flex h-8 w-8 items-center justify-center rounded-lg text-lg transition-colors duration-150 hover:bg-secondary"
              >
                {e.emoji}
              </button>
            ))}
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
