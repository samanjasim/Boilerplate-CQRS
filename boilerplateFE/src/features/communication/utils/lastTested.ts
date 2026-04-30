import { formatDistanceToNowStrict, differenceInHours, differenceInDays } from 'date-fns';

export type LastTestedTone = 'fresh' | 'today' | 'week' | 'older' | 'never';

export interface LastTestedState {
  tone: LastTestedTone;
  /** Display string, e.g. "5 minutes ago", or null when never tested. */
  label: string | null;
  /** Tailwind class string for the chip background + text. */
  chipClass: string;
}

const CHIP_CLASS_BY_TONE: Record<LastTestedTone, string> = {
  fresh:
    'bg-[var(--color-emerald-500)]/10 text-[var(--color-emerald-700)] dark:text-[var(--color-emerald-300)]',
  today: 'bg-muted text-muted-foreground',
  week:
    'bg-[var(--state-warn-bg)] text-[var(--state-warn-fg)] border border-[var(--state-warn-border)]',
  older: 'bg-muted text-muted-foreground',
  never: 'bg-muted text-muted-foreground',
};

export function deriveLastTestedState(
  lastTestedAt: string | null | undefined,
  now: Date = new Date(),
): LastTestedState {
  if (!lastTestedAt) {
    return { tone: 'never', label: null, chipClass: CHIP_CLASS_BY_TONE.never };
  }

  const tested = new Date(lastTestedAt);
  const hours = differenceInHours(now, tested);
  const days = differenceInDays(now, tested);

  let tone: LastTestedTone;
  if (hours < 1) tone = 'fresh';
  else if (hours < 24) tone = 'today';
  else if (days < 7) tone = 'week';
  else tone = 'older';

  return {
    tone,
    label: formatDistanceToNowStrict(tested, { addSuffix: true }),
    chipClass: CHIP_CLASS_BY_TONE[tone],
  };
}
