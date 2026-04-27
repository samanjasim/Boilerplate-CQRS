import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { UserPlus, Lock, User, Eye, EyeOff } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { acceptInviteSchema, type AcceptInviteFormData } from '@/lib/validation';
import { useAcceptInvite } from '../api';
import { ROUTES } from '@/config';

export default function AcceptInvitePage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';

  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const { mutate: acceptInvite, isPending } = useAcceptInvite();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<AcceptInviteFormData>({
    resolver: zodResolver(acceptInviteSchema),
    defaultValues: {
      firstName: '',
      lastName: '',
      password: '',
      confirmPassword: '',
    },
  });

  const onSubmit = (data: AcceptInviteFormData) => {
    acceptInvite({
      token,
      firstName: data.firstName,
      lastName: data.lastName,
      password: data.password,
      confirmPassword: data.confirmPassword,
    });
  };

  if (!token) {
    return (
      <div>
        <div className="mb-7">
          <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">
            Invitation
          </div>
          <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.12] font-display text-foreground">
            {t('invitations.acceptInvite')}
          </h2>
          <p className="mt-2 text-sm text-muted-foreground leading-[1.55]">{t('invitations.invalidToken')}</p>
        </div>
        <div className="text-center">
          <p className="mb-4 text-sm text-muted-foreground">{t('invitations.invalidTokenDesc')}</p>
          <Link to={ROUTES.LOGIN} className="font-medium text-primary hover:underline text-sm">
            {t('auth.backToLogin')}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-7">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">
          Invitation
        </div>
        <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.12] font-display text-foreground">
          {t('invitations.acceptInvite')}
        </h2>
        <p className="mt-2 text-sm text-muted-foreground leading-[1.55]">{t('invitations.acceptInviteDesc')}</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="firstName">{t('auth.firstName')}</Label>
                <div className="relative">
                  <User className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="firstName"
                    type="text"
                    placeholder={t('auth.firstName')}
                    className="ps-10"
                    {...register('firstName')}
                  />
                </div>
                {errors.firstName && (
                  <p className="text-sm text-destructive">{errors.firstName.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="lastName">{t('auth.lastName')}</Label>
                <div className="relative">
                  <User className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="lastName"
                    type="text"
                    placeholder={t('auth.lastName')}
                    className="ps-10"
                    {...register('lastName')}
                  />
                </div>
                {errors.lastName && (
                  <p className="text-sm text-destructive">{errors.lastName.message}</p>
                )}
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="password">{t('auth.password')}</Label>
              <div className="relative">
                <Lock className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  placeholder={t('auth.createPassword')}
                  className="ps-10 pe-10"
                  {...register('password')}
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
              {errors.password && (
                <p className="text-sm text-destructive">{errors.password.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="confirmPassword">{t('auth.confirmPassword')}</Label>
              <div className="relative">
                <Lock className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="confirmPassword"
                  type={showConfirmPassword ? 'text' : 'password'}
                  placeholder={t('auth.confirmYourPassword')}
                  className="ps-10 pe-10"
                  {...register('confirmPassword')}
                />
                <button
                  type="button"
                  aria-label={showConfirmPassword ? t('common.hidePassword') : t('common.showPassword')}
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  className="absolute end-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                >
                  {showConfirmPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              {errors.confirmPassword && (
                <p className="text-sm text-destructive">{errors.confirmPassword.message}</p>
              )}
            </div>

        <Button type="submit" className="w-full" disabled={isPending}>
          <UserPlus className="me-2 h-4 w-4" />
          {isPending ? t('common.loading') : t('invitations.acceptInvite')}
        </Button>
      </form>

      <div className="mt-6 pt-4 border-t border-border/30 text-center text-sm text-muted-foreground">
        <Link to={ROUTES.LOGIN} className="font-medium text-primary hover:underline">
          {t('auth.backToLogin')}
        </Link>
      </div>
    </div>
  );
}
