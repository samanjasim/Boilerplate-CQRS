import { Outlet } from 'react-router-dom';
import { useUIStore, selectSidebarCollapsed } from '@/stores';
import { cn } from '@/lib/utils';
import { Sidebar } from './Sidebar';
import { Header } from './Header';
import { useOnboardingCheck } from '@/features/onboarding/hooks/useOnboardingCheck';
import { OnboardingWizard } from '@/features/onboarding/components/OnboardingWizard';
import { RouteErrorBoundary } from '@/components/common';

export function MainLayout() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const { showOnboarding, completeOnboarding, remindLater } = useOnboardingCheck();

  if (showOnboarding) {
    return (
      <OnboardingWizard
        onComplete={completeOnboarding}
        onRemindLater={remindLater}
      />
    );
  }

  return (
    <div className="aurora-canvas min-h-screen bg-background" data-page-style="dense">
      <Sidebar />
      <Header />
      <main
        className={cn(
          'pt-16 transition-all duration-300',
          isCollapsed ? 'ltr:pl-16 rtl:pr-16' : 'ltr:pl-60 rtl:pr-60'
        )}
      >
        <div className="p-8">
          <RouteErrorBoundary>
            <Outlet />
          </RouteErrorBoundary>
        </div>
      </main>
    </div>
  );
}
