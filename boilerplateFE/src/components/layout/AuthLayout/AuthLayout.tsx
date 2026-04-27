import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LanguageSwitcher, ThemeToggle } from '@/components/common';

const APP_NAME = import.meta.env.VITE_APP_NAME || 'Starter';

export function AuthLayout() {
  const { t } = useTranslation();

  return (
    <div className="aurora-canvas relative flex h-screen bg-background overflow-hidden">
      <div aria-hidden className="aurora-layer-2" />

      {/* Left side — Brand panel */}
      <div className="hidden lg:flex lg:w-1/2 flex-col items-center justify-center p-12 relative">
        <div className="max-w-md text-center relative z-10 animate-fade-up">
          <div className="text-[10px] font-bold uppercase tracking-[0.2em] text-primary mb-6 inline-flex items-center gap-2">
            <span className="pulse-dot" />
            Open source · Production-grade
          </div>

          <div className="mb-8 mx-auto inline-flex h-16 w-16 items-center justify-center rounded-2xl btn-primary-gradient glow-primary-lg">
            <span className="font-display text-2xl font-bold text-primary-foreground">
              {APP_NAME.charAt(0)}
            </span>
          </div>

          <h1 className="mb-4 text-[36px] sm:text-[42px] font-extralight tracking-[-0.04em] leading-[1.08] text-foreground font-display">
            {t('auth.brandTitle')}
          </h1>

          <p className="text-sm text-muted-foreground leading-[1.6] max-w-[360px] mx-auto">
            {t('auth.brandSubtitle')}
          </p>
        </div>
      </div>

      {/* Right side — Auth form */}
      <div className="relative flex w-full flex-col lg:w-1/2 z-[1]">
        <div className="flex items-center justify-end gap-1 px-6 py-4">
          <LanguageSwitcher variant="text" />
          <ThemeToggle variant="text" />
        </div>
        <div className="flex-1 overflow-y-auto">
          <div className="flex min-h-full items-center justify-center px-6 lg:px-8 pb-8">
            <div className="surface-glass-strong w-full max-w-md rounded-2xl p-8 shadow-float border border-border/40">
              <Outlet />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
