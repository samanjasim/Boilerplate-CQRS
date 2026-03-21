import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Blocks } from 'lucide-react';
import { LanguageSwitcher, ThemeToggle } from '@/components/common';

export function AuthLayout() {
  const { t } = useTranslation();

  return (
    <div className="flex h-screen bg-background">
      {/* Left side - Branding */}
      <div className="hidden lg:flex lg:w-1/2 flex-col items-center justify-center bg-gradient-to-br from-primary-600 via-primary-700 to-primary-900 p-12 relative">
        <div className="max-w-md text-center">
          <div className="mb-8 inline-flex h-20 w-20 items-center justify-center rounded-2xl bg-white/10 shadow-lg">
            <Blocks className="h-10 w-10 text-white" />
          </div>
          <h1 className="mb-4 text-4xl font-bold text-white">
            {t('auth.brandTitle')}
          </h1>
          <p className="text-lg text-primary-100">
            {t('auth.brandSubtitle')}
          </p>
        </div>

        {/* Decorative elements */}
        <div className="absolute bottom-0 left-0 h-1/3 w-1/2 bg-gradient-to-t from-white/5 to-transparent" />
      </div>

      {/* Right side - Auth form */}
      <div className="relative flex w-full flex-col lg:w-1/2">
        {/* Top bar with language + theme toggles */}
        <div className="flex items-center justify-end gap-2 px-6 py-4">
          <LanguageSwitcher variant="text" />
          <ThemeToggle variant="text" />
        </div>

        {/* Scrollable form area */}
        <div className="flex-1 overflow-y-auto">
          <div className="flex min-h-full items-center justify-center px-8 pb-8">
            <div className="w-full max-w-md">
              <Outlet />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
