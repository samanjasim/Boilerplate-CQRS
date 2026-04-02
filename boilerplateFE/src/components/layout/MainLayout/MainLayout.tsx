import { useState, useCallback } from 'react';
import { Outlet } from 'react-router-dom';
import { useUIStore, selectSidebarCollapsed } from '@/stores';
import { cn } from '@/lib/utils';
import { Sidebar } from './Sidebar';
import { Header } from './Header';
import { useOnboardingCheck } from '@/features/onboarding/hooks/useOnboardingCheck';
import { OnboardingWizard } from '@/features/onboarding/components/OnboardingWizard';

export function MainLayout() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const { showOnboarding, dismissOnboarding } = useOnboardingCheck();
  const [wizardDismissed, setWizardDismissed] = useState(false);

  const handleOnboardingComplete = useCallback(() => {
    dismissOnboarding();
    setWizardDismissed(true);
  }, [dismissOnboarding]);

  if (showOnboarding && !wizardDismissed) {
    return <OnboardingWizard onComplete={handleOnboardingComplete} />;
  }

  return (
    <div className="min-h-screen bg-background">
      <Sidebar />
      <Header />
      <main
        className={cn(
          'pt-16 transition-all duration-300',
          isCollapsed ? 'ltr:pl-16 rtl:pr-16' : 'ltr:pl-60 rtl:pr-60'
        )}
      >
        <div className="p-8">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
