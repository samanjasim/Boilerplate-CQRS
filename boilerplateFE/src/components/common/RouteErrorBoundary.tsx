import type { ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { AlertTriangle, RefreshCw, Home } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ErrorBoundary } from './ErrorBoundary';
import { ROUTES } from '@/config';

function RouteErrorFallback() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  return (
    <Card className="mx-auto max-w-md">
      <CardContent className="flex flex-col items-center text-center py-10">
        <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-destructive/10">
          <AlertTriangle className="h-7 w-7 text-destructive" />
        </div>
        <h2 className="mb-2 text-lg font-semibold text-foreground">
          {t('common.somethingWentWrong')}
        </h2>
        <p className="mb-6 text-sm text-muted-foreground">
          {t('common.unexpectedError')}
        </p>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => window.location.reload()}>
            <RefreshCw className="h-4 w-4" />
            {t('common.tryAgain')}
          </Button>
          <Button onClick={() => navigate(ROUTES.DASHBOARD)}>
            <Home className="h-4 w-4" />
            {t('common.home')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * Wraps the current route's element in an ErrorBoundary keyed on the
 * pathname so a feature crash leaves the layout (sidebar/header) intact
 * and resets automatically when the user navigates away.
 */
export function RouteErrorBoundary({ children }: { children: ReactNode }) {
  const location = useLocation();
  return (
    <ErrorBoundary key={location.pathname} fallback={<RouteErrorFallback />}>
      {children}
    </ErrorBoundary>
  );
}
