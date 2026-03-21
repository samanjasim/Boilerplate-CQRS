import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { RegisterForm } from '../components';
import { ROUTES } from '@/config';

export default function RegisterPage() {
  const { t } = useTranslation();

  return (
    <div>
      <div className="mb-8 text-center lg:text-left">
        <h2 className="text-2xl font-bold text-foreground">{t('auth.createAccount')}</h2>
        <p className="mt-2 text-muted-foreground">{t('auth.joinStarter')}</p>
      </div>

      <Card>
        <CardContent className="pt-6">
          <RegisterForm />

          <div className="mt-6 text-center text-sm text-muted-foreground">
            {t('auth.hasAccount')}{' '}
            <Link to={ROUTES.LOGIN} className="font-medium text-primary hover:underline">
              {t('auth.signInLink')}
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
