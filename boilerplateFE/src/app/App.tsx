import { useEffect, useState } from 'react';
import { AppProviders } from './providers';
import { AppRouter } from '@/routes';
import { useAuthStore, useUIStore } from '@/stores';
import { useTenantBranding } from '@/hooks';
import { storage, getTenantSlug } from '@/utils';
import { authApi } from '@/features/auth/api/auth.api';
import { tenantsApi } from '@/features/tenants/api/tenants.api';

function AppContent() {
  const { setUser, setLoading, logout } = useAuthStore();
  const { setActiveTenantId, setTenantSlug } = useUIStore();
  const [tenantReady, setTenantReady] = useState(false);

  useTenantBranding();

  // Step 1: Resolve subdomain
  useEffect(() => {
    const resolveSubdomain = async () => {
      const slug = getTenantSlug();
      if (slug) {
        try {
          const branding = await tenantsApi.getBranding(slug);
          if (branding && branding.status === 'Active') {
            setActiveTenantId(branding.tenantId);
            setTenantSlug(slug);
          } else {
            setActiveTenantId(null);
            setTenantSlug(null);
          }
        } catch {
          setActiveTenantId(null);
          setTenantSlug(null);
        }
      } else {
        setTenantSlug(null);
      }
      setTenantReady(true);
    };
    resolveSubdomain();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Step 2: Existing auth init — gated by tenantReady
  useEffect(() => {
    if (!tenantReady) return;

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
  }, [tenantReady, setUser, setLoading, logout]);

  return <AppRouter />;
}

export function App() {
  return (
    <AppProviders>
      <AppContent />
    </AppProviders>
  );
}
