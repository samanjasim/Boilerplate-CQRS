import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface PageHeaderProps {
  title?: string;
  subtitle?: string;
  backTo?: string;
  backLabel?: string;
  actions?: ReactNode;
}

export function PageHeader({ title, subtitle, backTo, backLabel, actions }: PageHeaderProps) {
  return (
    <div>
      {backTo && (
        <Link to={backTo}>
          <Button variant="ghost" size="sm">
            <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
            {backLabel}
          </Button>
        </Link>
      )}
      {(title || actions) && (
        <div className={`flex items-center justify-between ${backTo ? 'mt-2' : ''}`}>
          <div>
            {title && <h1 className="text-2xl font-bold text-foreground">{title}</h1>}
            {subtitle && <p className="text-muted-foreground">{subtitle}</p>}
          </div>
          {actions && <div className="shrink-0">{actions}</div>}
        </div>
      )}
    </div>
  );
}
