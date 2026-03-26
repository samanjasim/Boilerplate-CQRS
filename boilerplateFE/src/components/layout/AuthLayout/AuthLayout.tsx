import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Blocks } from 'lucide-react';
import { LanguageSwitcher, ThemeToggle } from '@/components/common';

export function AuthLayout() {
  const { t } = useTranslation();

  return (
    <div className="flex h-screen bg-background">
      {/* Left side - Branding */}
      <div className="hidden lg:flex lg:w-1/2 flex-col items-center justify-center gradient-hero p-12 relative overflow-hidden">
        <div className="max-w-md text-center relative z-10">
          <div className="mb-8 inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 backdrop-blur-sm">
            <Blocks className="h-8 w-8 text-white" />
          </div>
          <h1 className="mb-3 text-3xl font-bold text-white tracking-tight">
            {t('auth.brandTitle')}
          </h1>
          <p className="text-base text-white/70">
            {t('auth.brandSubtitle')}
          </p>
        </div>

        {/* Decorative elements */}
        <div className="absolute -right-32 -top-32 h-96 w-96 rounded-full bg-white/[0.04] blur-3xl" />
        <div className="absolute -bottom-32 -left-32 h-96 w-96 rounded-full bg-white/[0.03] blur-3xl" />
        <div className="absolute inset-0 opacity-[0.04] bg-[radial-gradient(circle,_rgba(255,255,255,0.8)_1px,_transparent_1px)] bg-[length:32px_32px]" />
      </div>

      {/* Right side - Auth form */}
      <div className="relative flex w-full flex-col lg:w-1/2">
        {/* Top bar with language + theme toggles */}
        <div className="flex items-center justify-end gap-1 px-6 py-4">
          <LanguageSwitcher variant="text" />
          <ThemeToggle variant="text" />
        </div>

        {/* Scrollable form area */}
        <div className="flex-1 overflow-y-auto">
          <div className="flex min-h-full items-center justify-center px-8 pb-8">
            <div className="w-full max-w-sm">
              <Outlet />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
