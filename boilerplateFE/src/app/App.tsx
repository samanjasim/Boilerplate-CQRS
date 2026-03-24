import { useEffect } from 'react';
import { AppProviders } from './providers';
import { AppRouter } from '@/routes';
import { useAuthStore } from '@/stores';
import { useTenantBranding } from '@/hooks';
import { storage } from '@/utils';
import { authApi } from '@/features/auth/api/auth.api';

function AppContent() {
  const { setUser, setLoading, logout } = useAuthStore();

  useTenantBranding();

  useEffect(() => {
    const initAuth = async () => {
      const accessToken = storage.getAccessToken();

      if (!accessToken) {
        setLoading(false);
        return;
      }

      try {
        const user = await authApi.getMe(accessToken);
        setUser(user);
        setLoading(false);
      } catch {
        storage.clearTokens();
        logout();
      }
    };

    initAuth();
  }, [setUser, setLoading, logout]);

  return <AppRouter />;
}

export function App() {
  return (
    <AppProviders>
      <AppContent />
    </AppProviders>
  );
}
