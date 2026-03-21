import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore, selectIsAuthenticated, selectIsLoading } from '@/stores';
import { ROUTES } from '@/config';
import { LoadingScreen } from '@/components/common';

interface LocationState {
  from?: { pathname: string };
}

export function GuestGuard() {
  const isAuthenticated = useAuthStore(selectIsAuthenticated);
  const isLoading = useAuthStore(selectIsLoading);
  const location = useLocation();

  if (isLoading) {
    return <LoadingScreen />;
  }

  if (isAuthenticated) {
    const state = location.state as LocationState | null;
    const from = state?.from?.pathname || ROUTES.DASHBOARD;
    return <Navigate to={from} replace />;
  }

  return <Outlet />;
}
