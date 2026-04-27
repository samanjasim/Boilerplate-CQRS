import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Mail, Lock, User, Building, Eye, EyeOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { registerTenantSchema, type RegisterTenantFormData } from '@/lib/validation';
import { useRegisterTenant } from '../api';
import { ROUTES } from '@/config';

export default function RegisterTenantPage() {
  const { t } = useTranslation();
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const { mutate: registerTenant, isPending } = useRegisterTenant();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterTenantFormData>({
    resolver: zodResolver(registerTenantSchema),
    defaultValues: {
      companyName: '',
      firstName: '',
      lastName: '',
      email: '',
      password: '',
      confirmPassword: '',
    },
  });

  const onSubmit = (data: RegisterTenantFormData) => {
    registerTenant(data);
  };

  return (
    <div>
      <div className="mb-7">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">
          Get started
        </div>
        <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.12] font-display text-foreground">
          {t('auth.createOrganization')}
        </h2>
        <p className="mt-2 text-sm text-muted-foreground leading-[1.55]">{t('auth.createOrganizationDesc')}</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="companyName">{t('auth.companyName')}</Label>
              <div className="relative">
                <Building className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="companyName"
                  placeholder="Acme Inc."
                  className="ps-10"
                  {...register('companyName')}
                />
              </div>
              {errors.companyName && (
                <p className="text-sm text-destructive">{errors.companyName.message}</p>
              )}
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="firstName">{t('auth.firstName')}</Label>
                <div className="relative">
                  <User className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="firstName"
                    placeholder="John"
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
                <Input
                  id="lastName"
                  placeholder="Doe"
                  {...register('lastName')}
                />
                {errors.lastName && (
                  <p className="text-sm text-destructive">{errors.lastName.message}</p>
                )}
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="email">{t('auth.email')}</Label>
              <div className="relative">
                <Mail className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="email"
                  type="email"
                  placeholder="john@example.com"
                  className="ps-10"
                  {...register('email')}
                />
              </div>
              {errors.email && (
                <p className="text-sm text-destructive">{errors.email.message}</p>
              )}
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
          {isPending ? t('auth.creatingAccount') : t('auth.signUp')}
        </Button>
      </form>

      <div className="mt-6 pt-4 border-t border-border/30 text-center text-sm text-muted-foreground">
        {t('auth.hasAccount')}{' '}
        <Link to={ROUTES.LOGIN} className="font-medium text-primary hover:underline">
          {t('auth.signInLink')}
        </Link>
      </div>
    </div>
  );
}
