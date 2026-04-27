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

  // Lock body scroll while the mobile drawer is open.
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
    <div
      className="aurora-canvas min-h-screen bg-background overflow-x-clip"
      data-page-style="dense"
    >
      <Sidebar />
      <Header />
      {/* Mobile drawer backdrop */}
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
          // pt = header top (14) + header height (48) + gap (8) = 70
          'pt-[70px] motion-safe:transition-all motion-safe:duration-300',
          // edge padding swaps with sidebar margin on lg+; flush on <lg
          'max-lg:px-3.5',
          isCollapsed
            ? 'lg:ltr:pl-[calc(4rem+1.75rem)] lg:rtl:pr-[calc(4rem+1.75rem)]'
            : 'lg:ltr:pl-[calc(15rem+1.75rem)] lg:rtl:pr-[calc(15rem+1.75rem)]',
          'lg:ltr:pr-3.5 lg:rtl:pl-3.5'
        )}
      >
        <div className="px-2 pb-6 pt-2">
          <RouteErrorBoundary>
            <Outlet />
          </RouteErrorBoundary>
        </div>
      </main>
    </div>
  );
}
