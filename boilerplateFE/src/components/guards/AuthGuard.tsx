import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore, selectIsAuthenticated, selectIsLoading } from '@/stores';
import { ROUTES } from '@/config';
import { LoadingScreen } from '@/components/common';
import { isSubdomainAccess, getMainDomainUrl } from '@/utils';

export function AuthGuard() {
  const isAuthenticated = useAuthStore(selectIsAuthenticated);
  const isLoading = useAuthStore(selectIsLoading);
  const location = useLocation();

  if (isLoading) {
    return <LoadingScreen />;
  }

  if (!isAuthenticated) {
    if (isSubdomainAccess()) {
      window.location.href = getMainDomainUrl() + '/login';
      return <LoadingScreen />;
    }
    return <Navigate to={ROUTES.LOGIN} state={{ from: location }} replace />;
  }

  return <Outlet />;
}
