import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LoginForm } from '../components';
import { ROUTES } from '@/config';

export default function LoginPage() {
  const { t } = useTranslation();

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-xl font-bold tracking-tight">{t('auth.welcomeBack')}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t('auth.signInContinue')}</p>
      </div>

      <div className="space-y-5">
        <LoginForm />

        <div className="text-center text-sm">
          <Link to={ROUTES.FORGOT_PASSWORD} className="font-medium text-primary hover:text-primary/80 transition-colors">
            {t('auth.forgotPassword')}
          </Link>
        </div>

        <div className="text-center text-sm text-muted-foreground">
          {t('auth.noAccount')}{' '}
          <Link to={ROUTES.REGISTER} className="font-medium text-primary hover:text-primary/80 transition-colors">
            {t('auth.createOne')}
          </Link>
        </div>
      </div>
    </div>
  );
}
