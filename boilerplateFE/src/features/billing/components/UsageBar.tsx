import { cn } from '@/lib/utils';

interface UsageBarProps {
  label: string;
  current: number;
  max: number;
  formatValue?: (n: number) => string;
  className?: string;
}

export function UsageBar({ label, current, max, formatValue, className }: UsageBarProps) {
  const pct = max > 0 ? Math.min(100, Math.round((current / max) * 100)) : 0;
  const fmt = formatValue ?? String;
  const isWarning = pct >= 80 && pct < 100;
  const isDanger = pct >= 100;

  return (
    <div className={cn('space-y-1.5', className)}>
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium text-foreground">{label}</span>
        <span className="text-muted-foreground">
          {fmt(current)} / {fmt(max)}
          <span className="ml-1.5 text-xs">({pct}%)</span>
        </span>
      </div>
      <div className="h-2 w-full rounded-full bg-secondary overflow-hidden">
        <div
          className={cn(
            'h-full rounded-full transition-all',
            isDanger ? 'bg-destructive' : isWarning ? 'bg-warning' : 'bg-primary',
          )}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}
