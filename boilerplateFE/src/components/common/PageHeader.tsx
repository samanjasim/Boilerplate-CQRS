import { ChevronRight } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link, NavLink } from 'react-router-dom';

import { cn } from '@/lib/utils';

export interface PageHeaderBreadcrumb {
  label: string;
  to?: string;
}

export interface PageHeaderTab {
  label: string;
  /** Router link target — used when tabs correspond to separate routes. */
  to?: string;
  /** Click handler — used when tabs are local state (no route change). */
  onClick?: () => void;
  /** Marks this tab as selected when using onClick mode. */
  active?: boolean;
  count?: number;
}

interface PageHeaderProps {
  title?: string;
  subtitle?: string;
  actions?: ReactNode;
  breadcrumbs?: PageHeaderBreadcrumb[];
  tabs?: PageHeaderTab[];
}

export function PageHeader({ title, subtitle, actions, breadcrumbs, tabs }: PageHeaderProps) {
  if (!title && !actions && !breadcrumbs?.length && !tabs?.length) return null;

  return (
    <div>
      {breadcrumbs && breadcrumbs.length > 0 && (
        <nav
          aria-label="Breadcrumb"
          className="mb-2 flex flex-wrap items-center gap-1.5 text-sm text-muted-foreground"
        >
          {breadcrumbs.map((crumb, idx) => {
            const isLast = idx === breadcrumbs.length - 1;
            const node = crumb.to && !isLast ? (
              <Link
                to={crumb.to}
                className="hover:text-foreground motion-safe:transition-colors motion-safe:duration-150"
              >
                {crumb.label}
              </Link>
            ) : (
              <span className={isLast ? 'font-medium text-primary' : undefined}>{crumb.label}</span>
            );
            return (
              <span key={`${crumb.label}-${idx}`} className="flex items-center gap-1.5">
                {node}
                {!isLast && (
                  <ChevronRight className="h-3 w-3 opacity-50 rtl:rotate-180" aria-hidden />
                )}
              </span>
            );
          })}
        </nav>
      )}

      {(title || actions) && (
        <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0">
            {title && (
              <h1
                className={cn(
                  'font-display text-3xl lg:text-[32px] font-extralight tracking-tight leading-[1.1]',
                  'bg-clip-text text-transparent',
                  'bg-[linear-gradient(95deg,_hsl(var(--foreground))_30%,_hsl(var(--primary))_100%)]'
                )}
              >
                {title}
              </h1>
            )}
            {subtitle && <p className="mt-1.5 text-sm text-muted-foreground">{subtitle}</p>}
          </div>
          {actions && <div className="shrink-0">{actions}</div>}
        </div>
      )}

      {tabs && tabs.length > 0 && (
        <div className="mt-5 -mx-2 px-2 border-b border-border/40">
          <nav className="flex gap-1" aria-label="Tabs">
            {tabs.map((tab) => {
              const commonClass = (isActive: boolean) =>
                cn(
                  'inline-flex items-center gap-2 px-4 py-2.5 text-sm font-medium',
                  'motion-safe:transition-colors motion-safe:duration-150',
                  isActive
                    ? 'border-b-2 border-primary text-foreground'
                    : 'border-b-2 border-transparent text-muted-foreground hover:text-foreground',
                );
              const badge = tab.count != null && (
                <span className="rounded-full bg-secondary px-2 py-0.5 text-xs font-normal">
                  {tab.count}
                </span>
              );

              if (tab.onClick) {
                return (
                  <button
                    key={tab.label}
                    type="button"
                    onClick={tab.onClick}
                    className={commonClass(!!tab.active)}
                  >
                    {tab.label}
                    {badge}
                  </button>
                );
              }

              return (
                <NavLink
                  key={tab.to ?? tab.label}
                  to={tab.to!}
                  end={false}
                  className={({ isActive }) => commonClass(isActive)}
                >
                  {tab.label}
                  {badge}
                </NavLink>
              );
            })}
          </nav>
        </div>
      )}
    </div>
  );
}
