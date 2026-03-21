import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { LoginForm } from '../components';
import { ROUTES } from '@/config';

export default function LoginPage() {
  const { t } = useTranslation();

  return (
    <div>
      <div className="mb-8 text-center lg:text-left">
        <h2 className="text-2xl font-bold text-foreground">{t('auth.welcomeBack')}</h2>
        <p className="mt-2 text-muted-foreground">{t('auth.signInContinue')}</p>
      </div>

      <Card>
        <CardContent className="pt-6">
          <LoginForm />

          <div className="mt-4 text-center text-sm">
            <Link to={ROUTES.FORGOT_PASSWORD} className="font-medium text-primary hover:underline">
              {t('auth.forgotPassword')}
            </Link>
          </div>

          <div className="mt-4 text-center text-sm text-muted-foreground">
            {t('auth.noAccount')}{' '}
            <Link to={ROUTES.REGISTER} className="font-medium text-primary hover:underline">
              {t('auth.createOne')}
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
