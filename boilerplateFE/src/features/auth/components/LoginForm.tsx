import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Mail, Lock, Eye, EyeOff, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { loginSchema, type LoginFormData } from '@/lib/validation';
import { useLogin } from '../api';

export function LoginForm() {
  const { t } = useTranslation();
  const [showPassword, setShowPassword] = useState(false);
  const [requiresTwoFactor, setRequiresTwoFactor] = useState(false);
  const [twoFactorCode, setTwoFactorCode] = useState('');
  const [useBackupCode, setUseBackupCode] = useState(false);
  const [savedCredentials, setSavedCredentials] = useState<LoginFormData | null>(null);
  const { mutate: login, isPending } = useLogin();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const onSubmit = (data: LoginFormData) => {
    login(data, {
      onSuccess: (response) => {
        if (response.requiresTwoFactor) {
          setRequiresTwoFactor(true);
          setSavedCredentials(data);
        }
      },
    });
  };

  const onSubmit2FA = () => {
    if (!savedCredentials || !twoFactorCode.trim()) return;
    login(
      {
        ...savedCredentials,
        twoFactorCode: twoFactorCode.trim(),
      },
      {
        onError: () => setTwoFactorCode(''),
      }
    );
  };

  const handleBack = () => {
    setRequiresTwoFactor(false);
    setTwoFactorCode('');
    setUseBackupCode(false);
    setSavedCredentials(null);
  };

  if (requiresTwoFactor) {
    return (
      <div className="space-y-4">
        <div className="flex items-center gap-2 text-primary mb-2">
          <ShieldCheck className="h-5 w-5" />
          <h3 className="font-semibold">{t('twoFactor.title')}</h3>
        </div>
        <p className="text-sm text-muted-foreground">
          {useBackupCode ? t('twoFactor.enterBackupCode') : t('twoFactor.codeRequired')}
        </p>

        <div className="space-y-2">
          <Label htmlFor="twoFactorCode">
            {useBackupCode ? t('twoFactor.backupCode') : t('twoFactor.code')}
          </Label>
          <Input
            id="twoFactorCode"
            type="text"
            placeholder={useBackupCode ? 'XXXXXXXX' : '000000'}
            maxLength={useBackupCode ? 8 : 6}
            value={twoFactorCode}
            onChange={(e) => setTwoFactorCode(useBackupCode ? e.target.value : e.target.value.replace(/[^0-9]/g, ''))}
            autoFocus
            className="text-center text-lg tracking-widest"
          />
        </div>

        <Button
          type="button"
          className="w-full"
          disabled={isPending || !twoFactorCode.trim()}
          onClick={onSubmit2FA}
        >
          {isPending ? t('auth.signingIn') : t('twoFactor.verify')}
        </Button>

        <div className="flex items-center justify-between">
          <button
            type="button"
            onClick={() => {
              setUseBackupCode(!useBackupCode);
              setTwoFactorCode('');
            }}
            className="text-sm font-medium text-primary hover:underline"
          >
            {useBackupCode ? t('twoFactor.useAuthenticator') : t('twoFactor.useBackupCode')}
          </button>
          <button
            type="button"
            onClick={handleBack}
            className="text-sm text-muted-foreground hover:underline"
          >
            {t('auth.backToLogin')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor="email">{t('auth.email')}</Label>
        <div className="relative">
          <Mail className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            id="email"
            type="email"
            placeholder={t('auth.enterEmail')}
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
            placeholder={t('auth.enterPassword')}
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

      <Button type="submit" className="w-full" disabled={isPending}>
        {isPending ? t('auth.signingIn') : t('auth.signIn')}
      </Button>
    </form>
  );
}
