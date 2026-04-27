import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LanguageSwitcher, ThemeToggle } from '@/components/common';
import { HeroLinesBackground } from '@/features/landing/components';

const APP_NAME = import.meta.env.VITE_APP_NAME || 'Starter';

export function AuthLayout() {
  const { t } = useTranslation();

  return (
    <div className="aurora-canvas relative min-h-screen bg-background overflow-hidden">
      <div aria-hidden className="aurora-layer-2" />
      <HeroLinesBackground />

      {/* Top meta — live indicator (left), language + theme (right) */}
      <header className="relative z-20 flex items-center justify-between px-5 sm:px-7 py-4">
        <div className="flex items-center gap-2 text-[10px] font-mono uppercase tracking-[0.22em] text-muted-foreground">
          <span className="pulse-dot" />
          <span className="hidden sm:inline">v1.0 · production-grade</span>
          <span className="sm:hidden">live</span>
        </div>
        <div className="flex items-center gap-1">
          <LanguageSwitcher variant="text" />
          <ThemeToggle variant="text" />
        </div>
      </header>

      {/* Centered content */}
      <main className="relative z-[5] flex items-center justify-center px-5 sm:px-6 pb-12 pt-2 sm:pt-6 min-h-[calc(100vh-72px)]">
        <div className="w-full max-w-md">
          {/* Brand block — animated logo + name */}
          <div className="text-center mb-7 animate-fade-up">
            <div className="relative mb-5 mx-auto inline-flex">
              {/* Outer pulsing halo ring */}
              <span
                aria-hidden
                className="absolute inset-0 rounded-2xl animate-brand-halo"
                style={{
                  background:
                    'radial-gradient(circle, color-mix(in srgb, var(--color-primary) 40%, transparent) 0%, transparent 70%)',
                  filter: 'blur(8px)',
                }}
              />
              <div className="relative h-14 w-14 flex items-center justify-center rounded-2xl btn-primary-gradient glow-primary-lg">
                <span className="font-display text-[22px] font-bold text-primary-foreground">
                  {APP_NAME.charAt(0)}
                </span>
              </div>
            </div>
            <div className="font-display text-[20px] font-medium text-foreground tracking-[-0.01em]">
              {APP_NAME}
            </div>
            <div className="text-[11px] text-muted-foreground mt-1 font-mono">
              {t('auth.brandSubtitle')}
            </div>
          </div>

          {/* Glass card with form */}
          <div className="surface-glass-strong rounded-2xl p-7 sm:p-8 shadow-float border border-border/40 animate-fade-up-delay-1 relative">
            {/* Top accent — subtle gradient hairline at the card top */}
            <div
              aria-hidden
              className="absolute top-0 inset-x-8 h-px"
              style={{
                background:
                  'linear-gradient(90deg, transparent 0%, color-mix(in srgb, var(--color-primary) 40%, transparent) 50%, transparent 100%)',
              }}
            />
            <Outlet />
          </div>

          {/* Bottom meta */}
          <div className="mt-6 flex items-center justify-center gap-2 text-[10px] font-mono uppercase tracking-[0.18em] text-muted-foreground/70 animate-fade-up-delay-2">
            <span>Multi-tenant CQRS</span>
            <span className="opacity-50">·</span>
            <span>.NET 10 · React 19</span>
            <span className="opacity-50">·</span>
            <span>Apache-2.0</span>
          </div>
        </div>
      </main>
    </div>
  );
}
