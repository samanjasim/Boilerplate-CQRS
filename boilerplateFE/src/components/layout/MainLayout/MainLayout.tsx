import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation } from 'react-router-dom';

import { RouteErrorBoundary, ScrollToTopOnNavigate } from '@/components/common';
import { OnboardingWizard } from '@/features/onboarding/components/OnboardingWizard';
import { useOnboardingCheck } from '@/features/onboarding/hooks/useOnboardingCheck';
import { cn } from '@/lib/utils';
import { selectSidebarCollapsed, selectSidebarOpen, useUIStore } from '@/stores';

import { CommandPalette } from './CommandPalette';
import { Header } from './Header';
import { MorePanel } from './MorePanel';
import { NavOverflowProvider } from './NavOverflowProvider';
import { Sidebar } from './Sidebar';

export function MainLayout() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
  const { showOnboarding, completeOnboarding, remindLater } = useOnboardingCheck();
  const { t } = useTranslation();
  const location = useLocation();

  // Lock body scroll while the mobile drawer is open.
  useEffect(() => {
    if (!sidebarOpen) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previous;
    };
  }, [sidebarOpen]);

  // Global mod+K opens the command palette. Skipped while focus is in an editable field.
  useEffect(() => {
    const isEditable = (el: EventTarget | null) => {
      if (!(el instanceof HTMLElement)) return false;
      const tag = el.tagName;
      return (
        tag === 'INPUT' ||
        tag === 'TEXTAREA' ||
        tag === 'SELECT' ||
        el.isContentEditable
      );
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && (e.key === 'k' || e.key === 'K')) {
        if (isEditable(e.target)) return;
        e.preventDefault();
        useUIStore.getState().toggleCommandPalette();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  // Auto-close palette on navigation (defensive — clicks already close it).
  useEffect(() => {
    if (useUIStore.getState().commandPaletteOpen) {
      useUIStore.getState().setCommandPaletteOpen(false);
    }
  }, [location.pathname]);

  if (showOnboarding) {
    return (
      <OnboardingWizard onComplete={completeOnboarding} onRemindLater={remindLater} />
    );
  }

  return (
    <NavOverflowProvider>
    <div
      className="aurora-canvas min-h-screen bg-background overflow-clip"
      data-page-style="dense"
    >
      <ScrollToTopOnNavigate />
      <Sidebar />
      <MorePanel />
      <Header />
      <CommandPalette />
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
    </NavOverflowProvider>
  );
}
