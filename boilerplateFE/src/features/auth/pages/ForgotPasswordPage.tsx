import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Mail, Lock, Eye, EyeOff, KeyRound } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  forgotPasswordSchema,
  resetPasswordSchema,
  type ForgotPasswordFormData,
  type ResetPasswordFormData,
} from '@/lib/validation';
import { useForgotPassword, useResetPassword } from '../api';
import { ROUTES } from '@/config';

export default function ForgotPasswordPage() {
  const { t } = useTranslation();
  const [step, setStep] = useState<1 | 2>(1);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const { mutate: forgotPassword, isPending: isSending } = useForgotPassword();
  const { mutate: resetPassword, isPending: isResetting } = useResetPassword();

  const emailForm = useForm<ForgotPasswordFormData>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
  });

  const resetForm = useForm<ResetPasswordFormData>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      email: '',
      code: '',
      newPassword: '',
      confirmNewPassword: '',
    },
  });

  const onEmailSubmit = (data: ForgotPasswordFormData) => {
    forgotPassword(data, {
      onSuccess: () => {
        resetForm.setValue('email', data.email);
        setStep(2);
      },
    });
  };

  const onResetSubmit = (data: ResetPasswordFormData) => {
    resetPassword(data);
  };

  return (
    <div>
      <div className="mb-7">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">
          {step === 1 ? 'Recovery' : 'Reset'}
        </div>
        <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.12] font-display text-foreground">
          {step === 1 ? t('auth.forgotPassword') : t('auth.resetPassword')}
        </h2>
        <p className="mt-2 text-sm text-muted-foreground leading-[1.55]">
          {step === 1 ? t('auth.forgotPasswordDesc') : t('auth.enterResetCode')}
        </p>
      </div>

      <div>
          {step === 1 ? (
            <form onSubmit={emailForm.handleSubmit(onEmailSubmit)} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="email">{t('auth.email')}</Label>
                <div className="relative">
                  <Mail className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="email"
                    type="email"
                    placeholder={t('auth.enterEmail')}
                    className="ps-10"
                    {...emailForm.register('email')}
                  />
                </div>
                {emailForm.formState.errors.email && (
                  <p className="text-sm text-destructive">
                    {emailForm.formState.errors.email.message}
                  </p>
                )}
              </div>

              <Button type="submit" className="w-full" disabled={isSending}>
                {isSending ? t('common.loading') : t('auth.sendResetCode')}
              </Button>
            </form>
          ) : (
            <form onSubmit={resetForm.handleSubmit(onResetSubmit)} className="space-y-4">
              <input type="hidden" {...resetForm.register('email')} />

              <div className="space-y-2">
                <Label htmlFor="code">{t('auth.resetCode')}</Label>
                <div className="relative">
                  <KeyRound className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="code"
                    type="text"
                    placeholder="000000"
                    maxLength={6}
                    className="ps-10"
                    {...resetForm.register('code')}
                  />
                </div>
                {resetForm.formState.errors.code && (
                  <p className="text-sm text-destructive">
                    {resetForm.formState.errors.code.message}
                  </p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="newPassword">{t('auth.newPassword')}</Label>
                <div className="relative">
                  <Lock className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="newPassword"
                    type={showPassword ? 'text' : 'password'}
                    placeholder={t('auth.newPassword')}
                    className="ps-10 pe-10"
                    {...resetForm.register('newPassword')}
                  />
                  <button
                    type="button"
                    aria-label={showPassword ? t('common.hidePassword') : t('common.showPassword')}
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute end-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                  >
                    {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  </button>
                </div>
                {resetForm.formState.errors.newPassword && (
                  <p className="text-sm text-destructive">
                    {resetForm.formState.errors.newPassword.message}
                  </p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="confirmNewPassword">{t('auth.confirmNewPassword')}</Label>
                <div className="relative">
                  <Lock className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="confirmNewPassword"
                    type={showConfirmPassword ? 'text' : 'password'}
                    placeholder={t('auth.confirmNewPassword')}
                    className="ps-10 pe-10"
                    {...resetForm.register('confirmNewPassword')}
                  />
                  <button
                    type="button"
                    aria-label={showConfirmPassword ? t('common.hidePassword') : t('common.showPassword')}
                    onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                    className="absolute end-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                  >
                    {showConfirmPassword ? (
                      <EyeOff className="h-4 w-4" />
                    ) : (
                      <Eye className="h-4 w-4" />
                    )}
                  </button>
                </div>
                {resetForm.formState.errors.confirmNewPassword && (
                  <p className="text-sm text-destructive">
                    {resetForm.formState.errors.confirmNewPassword.message}
                  </p>
                )}
              </div>

              <Button type="submit" className="w-full" disabled={isResetting}>
                {isResetting ? t('common.loading') : t('auth.resetPassword')}
              </Button>
            </form>
          )}

          <div className="mt-6 pt-4 border-t border-border/30 text-center text-sm text-muted-foreground">
            <Link to={ROUTES.LOGIN} className="font-medium text-primary hover:underline">
              {t('auth.backToLogin')}
            </Link>
          </div>
      </div>
    </div>
  );
}
