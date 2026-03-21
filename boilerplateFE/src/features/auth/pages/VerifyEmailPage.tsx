import { useState, useEffect, useCallback } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { KeyRound, Mail } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { verifyEmailSchema, type VerifyEmailFormData } from '@/lib/validation';
import { useVerifyEmail, useSendEmailVerification } from '../api';
import { ROUTES } from '@/config';

export default function VerifyEmailPage() {
  const { t } = useTranslation();
  const location = useLocation();
  const stateEmail = (location.state as { email?: string })?.email ?? '';

  const [email, setEmail] = useState(stateEmail);
  const [emailInput, setEmailInput] = useState('');
  const [cooldown, setCooldown] = useState(0);

  const { mutate: verifyEmail, isPending: isVerifying } = useVerifyEmail();
  const { mutate: sendCode, isPending: isSending } = useSendEmailVerification();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<VerifyEmailFormData>({
    resolver: zodResolver(verifyEmailSchema),
    defaultValues: { code: '' },
  });

  useEffect(() => {
    if (cooldown <= 0) return;
    const timer = setInterval(() => {
      setCooldown((prev) => prev - 1);
    }, 1000);
    return () => clearInterval(timer);
  }, [cooldown]);

  const onSubmit = (data: VerifyEmailFormData) => {
    verifyEmail({ email, code: data.code });
  };

  const handleResend = useCallback(() => {
    sendCode(
      { email },
      {
        onSuccess: () => {
          setCooldown(60);
        },
      }
    );
  }, [sendCode, email]);

  const handleEmailSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (emailInput.trim()) {
      setEmail(emailInput.trim());
    }
  };

  // If no email yet, show email input form
  if (!email) {
    return (
      <div>
        <div className="mb-8 text-center lg:text-left">
          <h2 className="text-2xl font-bold text-foreground">{t('auth.verifyEmail')}</h2>
          <p className="mt-2 text-muted-foreground">{t('auth.verifyEmailDesc')}</p>
        </div>

        <Card>
          <CardContent className="pt-6">
            <form onSubmit={handleEmailSubmit} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="email">{t('auth.email')}</Label>
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="email"
                    type="email"
                    placeholder={t('auth.enterEmail')}
                    className="pl-10"
                    value={emailInput}
                    onChange={(e) => setEmailInput(e.target.value)}
                    required
                  />
                </div>
              </div>

              <Button type="submit" className="w-full">
                {t('common.next')}
              </Button>
            </form>

            <div className="mt-6 text-center text-sm text-muted-foreground">
              <Link to={ROUTES.LOGIN} className="font-medium text-primary hover:underline">
                {t('auth.backToLogin')}
              </Link>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-8 text-center lg:text-left">
        <h2 className="text-2xl font-bold text-foreground">{t('auth.verifyEmail')}</h2>
        <p className="mt-2 text-muted-foreground">{t('auth.verifyEmailDesc')}</p>
      </div>

      <Card>
        <CardContent className="pt-6">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="code">{t('auth.verificationCode')}</Label>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="code"
                  type="text"
                  placeholder="000000"
                  maxLength={6}
                  className="pl-10"
                  {...register('code')}
                />
              </div>
              {errors.code && (
                <p className="text-sm text-destructive">{errors.code.message}</p>
              )}
            </div>

            <Button type="submit" className="w-full" disabled={isVerifying}>
              {isVerifying ? t('common.loading') : t('auth.verifyEmail')}
            </Button>
          </form>

          <div className="mt-4 text-center">
            <Button
              variant="ghost"
              size="sm"
              onClick={handleResend}
              disabled={cooldown > 0 || isSending}
            >
              {cooldown > 0
                ? t('auth.resendIn', { seconds: cooldown })
                : t('auth.resendCode')}
            </Button>
          </div>

          <div className="mt-6 text-center text-sm text-muted-foreground">
            <Link to={ROUTES.LOGIN} className="font-medium text-primary hover:underline">
              {t('auth.backToLogin')}
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
