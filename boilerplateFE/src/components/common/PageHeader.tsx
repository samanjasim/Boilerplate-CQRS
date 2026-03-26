import type { ReactNode } from 'react';

interface PageHeaderProps {
  title?: string;
  subtitle?: string;
  actions?: ReactNode;
}

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
  if (!title && !actions) return null;

  return (
    <div className="flex items-center justify-between">
      <div>
        {title && <h1 className="text-2xl font-bold tracking-tight [color:var(--active-text)]">{title}</h1>}
        {subtitle && <p className="text-sm text-muted-foreground mt-0.5">{subtitle}</p>}
      </div>
      {actions && <div className="shrink-0">{actions}</div>}
    </div>
  );
}
