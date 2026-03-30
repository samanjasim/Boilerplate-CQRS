import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Blocks } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ROUTES } from '@/config';

export default function LandingPage() {
  const { t } = useTranslation();
  const appName = import.meta.env.VITE_APP_NAME || 'Starter';

  return (
    <div className="relative flex min-h-screen items-center justify-center gradient-hero overflow-hidden">
      {/* Decorative blur circles */}
      <div className="absolute -top-24 -left-24 h-96 w-96 rounded-full bg-white/10 blur-3xl" />
      <div className="absolute -bottom-24 -right-24 h-96 w-96 rounded-full bg-white/5 blur-3xl" />
      <div className="absolute bottom-0 left-0 h-1/3 w-1/2 bg-gradient-to-t from-white/5 to-transparent" />

      {/* Content */}
      <div className="relative z-10 flex flex-col items-center text-center px-6">
        <div className="mb-8 inline-flex h-20 w-20 items-center justify-center rounded-2xl bg-white/10 shadow-lg">
          <Blocks className="h-10 w-10 text-white" />
        </div>

        <h1 className="mb-4 text-5xl font-bold text-white">
          {t('landing.title', { appName })}
        </h1>

        <p className="mb-10 max-w-lg text-lg text-white/80">
          {t('landing.subtitle')}
        </p>

        <div className="flex items-center gap-4">
          <Button
            asChild
            size="lg"
            className="bg-white text-foreground hover:bg-white/90 shadow-lg"
          >
            <Link to={ROUTES.REGISTER_TENANT}>{t('landing.getStarted')}</Link>
          </Button>
          <Button
            asChild
            variant="outline"
            size="lg"
            className="border-white/30 bg-white/10 text-white hover:bg-white/15"
          >
            <Link to={ROUTES.LOGIN}>{t('landing.signIn')}</Link>
          </Button>
        </div>
      </div>
    </div>
  );
}
