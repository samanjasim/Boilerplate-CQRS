import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LoginForm } from '../components';
import { ROUTES } from '@/config';

export default function LoginPage() {
  const { t } = useTranslation();

  return (
    <div>
      <div className="mb-7">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2 inline-flex items-center gap-2">
          <span className="pulse-dot" />
          {t('auth.signIn')}
        </div>
        <h2 className="text-[28px] font-light tracking-[-0.025em] leading-[1.12] font-display text-foreground">
          <span className="gradient-text font-medium">{t('auth.welcomeBack')}</span>
        </h2>
        <p className="mt-2 text-sm text-muted-foreground leading-[1.55]">{t('auth.signInContinue')}</p>
      </div>

      <div className="space-y-5">
        <LoginForm />

        <div className="text-center text-sm">
          <Link to={ROUTES.FORGOT_PASSWORD} className="font-medium text-primary hover:text-primary/80 transition-colors">
            {t('auth.forgotPassword')}
          </Link>
        </div>

        <div className="text-center text-sm text-muted-foreground pt-4 border-t border-border/30">
          {t('auth.noAccount')}{' '}
          <Link to={ROUTES.REGISTER_TENANT} className="font-medium text-primary hover:text-primary/80 transition-colors">
            {t('auth.createOne')}
          </Link>
        </div>
      </div>
    </div>
  );
}
