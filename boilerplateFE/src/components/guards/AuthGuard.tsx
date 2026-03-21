import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore, selectIsAuthenticated, selectIsLoading } from '@/stores';
import { ROUTES } from '@/config';
import { LoadingScreen } from '@/components/common';

export function AuthGuard() {
  const isAuthenticated = useAuthStore(selectIsAuthenticated);
  const isLoading = useAuthStore(selectIsLoading);
  const location = useLocation();

  if (isLoading) {
    return <LoadingScreen />;
  }

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.LOGIN} state={{ from: location }} replace />;
  }

  return <Outlet />;
}
