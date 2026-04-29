import { type ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

type BadgeVariant = 'default' | 'secondary' | 'destructive' | 'outline';

export interface StatusHeaderChip {
  icon?: ReactNode;
  label: string;
  tinted?: boolean;
}

interface WorkflowStatusHeaderProps {
  title: string;
  status: string;
  statusVariant: BadgeVariant;
  chips?: StatusHeaderChip[];
  actions?: ReactNode;
  className?: string;
}

export function WorkflowStatusHeader({
  title,
  status,
  statusVariant,
  chips = [],
  actions,
  className,
}: WorkflowStatusHeaderProps) {
  return (
    <Card variant="glass" className={cn(className)}>
      <CardContent className="flex flex-wrap items-start justify-between gap-4 py-5">
        <div className="min-w-0 flex-1 space-y-2">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="truncate text-2xl font-semibold gradient-text">{title}</h1>
            <Badge variant={statusVariant}>{status}</Badge>
          </div>

          {chips.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 text-xs">
              {chips.map((chip, index) => (
                <span
                  key={`${chip.label}-${index}`}
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5',
                    chip.tinted
                      ? 'border-[var(--active-border)] text-[var(--tinted-fg)]'
                      : 'border-border text-muted-foreground',
                  )}
                >
                  {chip.icon}
                  {chip.label}
                </span>
              ))}
            </div>
          )}
        </div>

        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </CardContent>
    </Card>
  );
}
