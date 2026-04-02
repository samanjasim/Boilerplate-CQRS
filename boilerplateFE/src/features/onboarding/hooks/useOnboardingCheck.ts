import { useMemo } from 'react';
import { useAuthStore, selectUser } from '@/stores';
import { useUsers } from '@/features/users/api';

const STORAGE_KEY = 'onboarding-complete';

function isOnboardingDismissed(): boolean {
  try {
    return !!localStorage.getItem(STORAGE_KEY);
  } catch {
    return false;
  }
}

/**
 * Determines if the onboarding wizard should be shown.
 * Shows when: tenant user, tenant has 1 user (just registered), no logo, not dismissed.
 */
export function useOnboardingCheck() {
  const user = useAuthStore(selectUser);
  const { data: usersData } = useUsers({ enabled: !!user?.tenantId });

  const showOnboarding = useMemo(() => {
    if (!user?.tenantId) return false;
    if (isOnboardingDismissed()) return false;
    if (!usersData?.data) return false;

    const userCount = usersData?.data?.length ?? 0;
    const hasLogo = !!user.tenantLogoUrl;

    return userCount <= 1 && !hasLogo;
  }, [user, usersData]);

  const dismissOnboarding = () => {
    try {
      localStorage.setItem(STORAGE_KEY, 'true');
    } catch { /* ignore */ }
  };

  return { showOnboarding, dismissOnboarding };
}
