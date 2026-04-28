import { TrendingUp, type LucideIcon } from 'lucide-react';
import { useCountUp } from '@/hooks';

export type StatTone = 'copper' | 'emerald' | 'violet' | 'amber' | 'warn';

// eslint-disable-next-line react-refresh/only-export-components
export const STAT_TONE_BG: Record<StatTone, string> = {
  copper:
    'btn-primary-gradient glow-primary-sm',
  emerald:
    'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]',
  violet:
    'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]',
  amber:
    'bg-gradient-to-br from-[var(--color-amber-400)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_30%,transparent)]',
  warn:
    'bg-gradient-to-br from-[var(--color-amber-500)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_40%,transparent)]',
};

export interface StatCardProps {
  icon: LucideIcon;
  label: string;
  /** Number → animated count-up. String → displayed as-is (no animation). */
  value: number | string;
  delta?: string;
  tone: StatTone;
  /** SVG path data for a sparkline; viewBox 0 0 100 30. */
  spark?: string;
  /**
   * 'hero' — renders the value with gradient-text at a larger scale,
   * suitable for the primary metric in a strip.
   */
  variant?: 'default' | 'hero';
}

export function StatCard({
  icon: Icon,
  label,
  value,
  delta,
  tone,
  spark,
  variant = 'default',
}: StatCardProps) {
  const numericTarget = typeof value === 'number' ? value : 0;
  const { value: animatedValue } = useCountUp(numericTarget);
  const display =
    typeof value === 'number' ? animatedValue.toLocaleString() : value;

  const isHero = variant === 'hero';

  return (
    <div className="surface-glass hover-lift-card rounded-2xl p-5 border border-border/40">
      <div className="flex items-start justify-between gap-3 mb-3">
        <div
          className={`w-10 h-10 rounded-xl flex items-center justify-center text-white ${STAT_TONE_BG[tone]}`}
        >
          <Icon className="h-[18px] w-[18px]" strokeWidth={2} />
        </div>
        {spark && (
          <svg
            viewBox="0 0 100 30"
            className="h-7 w-20 shrink-0"
            preserveAspectRatio="none"
          >
            <defs>
              <linearGradient
                id={`stat-spark-${label}`}
                x1="0"
                x2="1"
                y1="0"
                y2="0"
              >
                <stop offset="0%" stopColor="var(--color-primary-700)" />
                <stop offset="100%" stopColor="var(--color-violet-500)" />
              </linearGradient>
            </defs>
            <path
              d={spark}
              fill="none"
              stroke={`url(#stat-spark-${label})`}
              strokeWidth="1.4"
              strokeLinecap="round"
              strokeLinejoin="round"
              opacity="0.45"
            />
            <path
              d={spark}
              pathLength={100}
              fill="none"
              stroke={`url(#stat-spark-${label})`}
              strokeWidth="1.8"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="spark-shimmer"
            />
          </svg>
        )}
      </div>

      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted-foreground mb-1">
        {label}
      </div>

      <div
        className={
          isHero
            ? 'text-[40px] font-light tracking-[-0.03em] leading-none font-display gradient-text mb-1.5 font-feature-settings'
            : 'text-[32px] font-light tracking-[-0.025em] leading-none font-display text-foreground mb-1.5 font-feature-settings'
        }
      >
        {display}
      </div>

      {delta && (
        <div className="text-[11px] font-mono text-[var(--color-accent-700)] dark:text-[var(--color-accent-300)] inline-flex items-center gap-1">
          <TrendingUp className="h-3 w-3" />
          {delta}
        </div>
      )}
    </div>
  );
}
