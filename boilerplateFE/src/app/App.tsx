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

  // Applies tenant primary color to CSS when user has tenantPrimaryColor
  useTenantBranding();

  // Step 1: Resolve subdomain → tenantId before auth init
  useEffect(() => {
    let cancelled = false;

    const resolveSubdomain = async () => {
      const slug = getTenantSlug();
      if (slug) {
        try {
          const branding = await tenantsApi.getBranding(slug);
          if (cancelled) return;
          if (branding && branding.status === 'Active') {
            setActiveTenantId(branding.tenantId);
            setTenantSlug(slug);
          } else {
            setActiveTenantId(null);
            setTenantSlug(null);
          }
        } catch {
          if (cancelled) return;
          setActiveTenantId(null);
          setTenantSlug(null);
        }
      } else {
        setTenantSlug(null);
      }
      if (!cancelled) setTenantReady(true);
    };

    resolveSubdomain();
    return () => { cancelled = true; };
  }, [setActiveTenantId, setTenantSlug]);

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
