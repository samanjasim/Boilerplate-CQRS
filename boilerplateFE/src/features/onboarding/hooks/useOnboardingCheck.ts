import { useMemo } from 'react';
import { useAuthStore, selectUser } from '@/stores';
import { useMarkTenantOnboarded } from '@/features/tenants/api';

const REMIND_LATER_KEY = 'onboarding-remind-later';
const REMIND_LATER_HOURS = 24;

function isRemindLaterActive(): boolean {
  try {
    const raw = localStorage.getItem(REMIND_LATER_KEY);
    if (!raw) return false;
    const remindUntil = Number(raw);
    if (Number.isNaN(remindUntil)) return false;
    return Date.now() < remindUntil;
  } catch {
    return false;
  }
}

function setRemindLater() {
  try {
    const expires = Date.now() + REMIND_LATER_HOURS * 60 * 60 * 1000;
    localStorage.setItem(REMIND_LATER_KEY, String(expires));
  } catch {
    /* ignore */
  }
}

function clearRemindLater() {
  try {
    localStorage.removeItem(REMIND_LATER_KEY);
  } catch {
    /* ignore */
  }
}

/**
 * Drives the post-registration onboarding wizard. Truth lives on the
 * backend (`Tenant.OnboardedAt`); a 24h "Remind me later" cookie suppresses
 * the wizard locally without writing to the BE so a user who clicks
 * "Remind me later" isn't permanently dismissed across devices.
 *
 * Wizard shows when:
 *   1. user is a tenant user (has tenantId), AND
 *   2. tenant has never been marked onboarded (`tenantOnboardedAt == null`), AND
 *   3. the local "remind me later" cookie has not fired or has expired.
 */
export function useOnboardingCheck() {
  const user = useAuthStore(selectUser);
  const { mutateAsync: markOnboarded } = useMarkTenantOnboarded();

  const showOnboarding = useMemo(() => {
    if (!user?.tenantId) return false;
    if (user.tenantOnboardedAt) return false;
    if (isRemindLaterActive()) return false;
    return true;
  }, [user]);

  const completeOnboarding = async () => {
    if (!user?.tenantId) return;
    clearRemindLater();
    await markOnboarded({ id: user.tenantId, onboarded: true });
  };

  const remindLater = () => {
    setRemindLater();
  };

  /** Re-runs the wizard. Used from Profile/Settings ("Run setup again"). */
  const reopenOnboarding = async () => {
    if (!user?.tenantId) return;
    clearRemindLater();
    await markOnboarded({ id: user.tenantId, onboarded: false });
  };

  return {
    showOnboarding,
    completeOnboarding,
    remindLater,
    reopenOnboarding,
  };
}
