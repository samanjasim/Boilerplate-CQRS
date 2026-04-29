import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import type { ReactNode } from 'react';

export interface MetricCardProps {
  label: string;
  /** Primary value rendered with `tabular-nums`. */
  value: ReactNode;
  /** Trailing fragment shown after the value (e.g., `/ 100`, `of 24 GB`). */
  secondary?: ReactNode;
  /** Subtle line under the label (e.g., `in flight`, `ready to download`). */
  eyebrow?: string;
  /** Apply `gradient-text` to the primary value. */
  emphasis?: boolean;
  /** Tailwind override hook for tinted cards (Active, Failed). */
  tone?: 'default' | 'active' | 'destructive';
  /** Optional inline glyph next to the value (e.g., spinner). */
  glyph?: ReactNode;
  /** Optional content rendered below the metric value, e.g. a progress bar. */
  children?: ReactNode;
  className?: string;
}

const TONE_CLASSES: Record<NonNullable<MetricCardProps['tone']>, string> = {
  default: '',
  active: 'border-primary/20 bg-[var(--active-bg)]/40',
  destructive: 'border-destructive/30 bg-destructive/10',
};

export function MetricCard({
  label,
  value,
  secondary,
  eyebrow,
  emphasis,
  tone = 'default',
  glyph,
  children,
  className,
}: MetricCardProps) {
  return (
    <Card variant="elevated" className={cn(TONE_CLASSES[tone], className)}>
      <CardContent className="pt-5">
        <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
        {eyebrow && (
          <div className="mt-0.5 text-[10px] uppercase tracking-[0.12em] text-muted-foreground/70">
            {eyebrow}
          </div>
        )}
        <div className="mt-2 flex items-baseline gap-2">
          <span
            className={cn(
              'text-2xl font-semibold tabular-nums',
              emphasis && 'gradient-text'
            )}
          >
            {value}
          </span>
          {glyph && <span className="text-muted-foreground">{glyph}</span>}
          {secondary && <span className="text-sm text-muted-foreground">{secondary}</span>}
        </div>
        {children}
      </CardContent>
    </Card>
  );
}
