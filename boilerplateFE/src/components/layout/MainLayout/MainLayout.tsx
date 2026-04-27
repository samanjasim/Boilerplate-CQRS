import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router-dom';

import { RouteErrorBoundary } from '@/components/common';
import { OnboardingWizard } from '@/features/onboarding/components/OnboardingWizard';
import { useOnboardingCheck } from '@/features/onboarding/hooks/useOnboardingCheck';
import { cn } from '@/lib/utils';
import { selectSidebarCollapsed, selectSidebarOpen, useUIStore } from '@/stores';

import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function MainLayout() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
  const { showOnboarding, completeOnboarding, remindLater } = useOnboardingCheck();
  const { t } = useTranslation();

  // Lock body scroll while the mobile drawer is open. The `lg:hidden` backdrop
  // already gates the visual; this prevents the page behind from scrolling on touch.
  useEffect(() => {
    if (!sidebarOpen) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previous;
    };
  }, [sidebarOpen]);

  if (showOnboarding) {
    return (
      <OnboardingWizard onComplete={completeOnboarding} onRemindLater={remindLater} />
    );
  }

  return (
    <div className="aurora-canvas min-h-screen bg-background" data-page-style="dense">
      <Sidebar />
      <Header />
      {/* Mobile drawer backdrop — only renders when open, only visible <lg */}
      {sidebarOpen && (
        <button
          type="button"
          aria-label={t('nav.toggle.close')}
          className="fixed inset-0 z-30 bg-background/60 backdrop-blur-sm lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}
      <main
        className={cn(
          'pt-16 motion-safe:transition-all motion-safe:duration-300',
          // No left padding on <lg — sidebar is a drawer, not in flow
          'pl-0',
          isCollapsed ? 'lg:ltr:pl-16 lg:rtl:pr-16' : 'lg:ltr:pl-60 lg:rtl:pr-60'
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
