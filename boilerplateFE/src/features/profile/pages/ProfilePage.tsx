import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import {
  Building,
  Calendar,
  CheckCircle,
  Clock,
  Eye,
  EyeOff,
  Mail,
  Pencil,
  Phone,
  Shield,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { InfoField, PageHeader, UserAvatar } from '@/components/common';
import { queryKeys } from '@/lib/query/keys';
import {
  changePasswordSchema,
  type ChangePasswordFormData,
  updateUserSchema,
  type UpdateUserFormData,
} from '@/lib/validation';
import { useAuthStore, selectUser } from '@/stores';
import { formatDate, formatDateTime } from '@/utils/format';
import { useChangePassword } from '@/features/auth/api';
import { useUpdateUser } from '@/features/users/api';
import { toast } from 'sonner';
import { LoginHistoryList } from '../components/LoginHistoryList';
import { NotificationPreferences } from '../components/NotificationPreferences';
import { SessionsList } from '../components/SessionsList';
import { TwoFactorSetup } from '../components/TwoFactorSetup';

export default function ProfilePage() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const queryClient = useQueryClient();

  const [showEditModal, setShowEditModal] = useState(false);
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const { mutate: changePassword, isPending: isChangingPassword } = useChangePassword();
  const { mutate: updateUser, isPending: isUpdating } = useUpdateUser();

  const {
    register: registerPassword,
    handleSubmit: handlePasswordSubmit,
    formState: { errors: passwordErrors },
    reset: resetPasswordForm,
  } = useForm<ChangePasswordFormData>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmNewPassword: '' },
  });

  const {
    register: registerProfile,
    handleSubmit: handleProfileSubmit,
    formState: { errors: profileErrors },
  } = useForm<UpdateUserFormData>({
    resolver: zodResolver(updateUserSchema(t)),
    defaultValues: {
      firstName: user?.firstName ?? '',
      lastName: user?.lastName ?? '',
      email: user?.email ?? '',
      phoneNumber: user?.phoneNumber ?? '',
    },
  });

  if (!user) return null;

  const onPasswordSubmit = (data: ChangePasswordFormData) => {
    changePassword(data, {
      onSuccess: () => {
        resetPasswordForm();
        setShowCurrentPassword(false);
        setShowNewPassword(false);
        setShowConfirmPassword(false);
      },
    });
  };

  const onProfileSubmit = (data: UpdateUserFormData) => {
    updateUser(
      { id: user.id, data: { ...data, phoneNumber: data.phoneNumber || null } },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() });
          toast.success(t('profile.profileUpdated'));
          setShowEditModal(false);
        },
      },
    );
  };

  return (
    <div className="space-y-6">
      <PageHeader title={t('profile.title')} />

      {/* ─── Hero identity card ─── */}
      <Card variant="glass" className="border border-border/40 overflow-hidden relative">
        <div
          aria-hidden
          className="pointer-events-none absolute -top-20 -right-16 h-64 w-64 rounded-full"
          style={{
            background:
              'radial-gradient(circle, color-mix(in srgb, var(--color-primary) 14%, transparent) 0%, transparent 70%)',
            filter: 'blur(24px)',
          }}
        />
        <CardContent className="py-7 px-7 relative">
          <div className="flex flex-col sm:flex-row sm:items-start gap-5">
            <UserAvatar firstName={user.firstName} lastName={user.lastName} size="xl" />

            <div className="flex-1 min-w-0">
              <h2 className="text-[26px] font-light tracking-[-0.02em] leading-none font-display gradient-text mb-1.5">
                {user.firstName} {user.lastName}
              </h2>
              <p className="text-[13px] text-muted-foreground mb-3">@{user.username}</p>

              <div className="flex flex-wrap gap-x-5 gap-y-1.5 text-[12px] text-muted-foreground">
                <span className="flex items-center gap-1.5">
                  <Mail className="h-3.5 w-3.5" />
                  {user.email}
                </span>
                {user.phoneNumber && (
                  <span className="flex items-center gap-1.5">
                    <Phone className="h-3.5 w-3.5" />
                    {user.phoneNumber}
                  </span>
                )}
                {user.tenantName && (
                  <span className="flex items-center gap-1.5">
                    <Building className="h-3.5 w-3.5" />
                    {user.tenantName}
                  </span>
                )}
                {user.createdAt && (
                  <span className="flex items-center gap-1.5">
                    <Calendar className="h-3.5 w-3.5" />
                    {t('profile.memberSince')} {formatDate(user.createdAt)}
                  </span>
                )}
                {user.lastLoginAt && (
                  <span className="flex items-center gap-1.5">
                    <Clock className="h-3.5 w-3.5" />
                    {t('profile.lastLogin')} {formatDateTime(user.lastLoginAt)}
                  </span>
                )}
              </div>

              {user.roles && user.roles.length > 0 && (
                <div className="flex flex-wrap gap-1.5 mt-3">
                  {user.roles.map((role) => (
                    <Badge key={role} variant="outline" className="text-[11px]">
                      <Shield className="h-3 w-3 mr-1 text-primary" />
                      {role}
                    </Badge>
                  ))}
                </div>
              )}
            </div>

            <Button variant="outline" size="sm" onClick={() => setShowEditModal(true)} className="shrink-0">
              <Pencil className="h-3.5 w-3.5" />
              {t('profile.editProfile')}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* ─── 2-column section grid ─── */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Account */}
        <Card variant="glass" className="border border-border/40">
          <CardContent className="py-6 px-6">
            <h3 className="text-[15px] font-semibold text-foreground tracking-tight mb-4">
              {t('profile.accountInfo')}
            </h3>
            <div className="space-y-4">
              <InfoField label={t('common.email')}>
                <div className="flex items-center gap-2">
                  {user.emailConfirmed ? (
                    <>
                      <CheckCircle className="h-4 w-4 text-[var(--color-accent-600)]" />
                      <Badge variant="healthy">{t('profile.emailVerified')}</Badge>
                    </>
                  ) : (
                    <>
                      <XCircle className="h-4 w-4 text-destructive" />
                      <Badge variant="failed">{t('profile.emailNotVerified')}</Badge>
                    </>
                  )}
                </div>
              </InfoField>
              <InfoField label={t('common.status')}>
                <Badge variant={user.status === 'Active' ? 'healthy' : 'failed'}>
                  {user.status || t('common.active')}
                </Badge>
              </InfoField>
              {user.phoneNumber && (
                <InfoField label={t('common.phone')}>
                  <div className="flex items-center gap-2">
                    <Phone className="h-4 w-4 text-muted-foreground" />
                    <span>{user.phoneNumber}</span>
                  </div>
                </InfoField>
              )}
              {user.tenantName && (
                <InfoField label={t('profile.tenant')}>
                  <div className="flex items-center gap-2">
                    <Building className="h-4 w-4 text-muted-foreground" />
                    <span>{user.tenantName}</span>
                  </div>
                </InfoField>
              )}
              {user.createdAt && (
                <InfoField label={t('profile.memberSince')}>
                  {formatDate(user.createdAt, 'long')}
                </InfoField>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Security */}
        <Card variant="glass" className="border border-border/40">
          <CardContent className="py-6 px-6">
            <h3 className="text-[15px] font-semibold text-foreground tracking-tight mb-4">
              {t('profile.changePassword')}
            </h3>
            <form onSubmit={handlePasswordSubmit(onPasswordSubmit)} className="space-y-3">
              <PasswordField
                label={t('profile.currentPassword')}
                show={showCurrentPassword}
                onToggle={() => setShowCurrentPassword((v) => !v)}
                error={passwordErrors.currentPassword?.message}
                {...registerPassword('currentPassword')}
              />
              <PasswordField
                label={t('profile.newPassword')}
                show={showNewPassword}
                onToggle={() => setShowNewPassword((v) => !v)}
                error={passwordErrors.newPassword?.message}
                {...registerPassword('newPassword')}
              />
              <PasswordField
                label={t('profile.confirmNewPassword')}
                show={showConfirmPassword}
                onToggle={() => setShowConfirmPassword((v) => !v)}
                error={passwordErrors.confirmNewPassword?.message}
                {...registerPassword('confirmNewPassword')}
              />
              <Button type="submit" size="sm" disabled={isChangingPassword} className="mt-1">
                {isChangingPassword ? t('common.saving') : t('profile.changePassword')}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>

      {/* ─── 2FA + Notifications (full-width) ─── */}
      <TwoFactorSetup user={user} />
      <NotificationPreferences />

      {/* ─── Sessions + Login History ─── */}
      <div className="grid gap-4 md:grid-cols-2">
        <SessionsList />
        <LoginHistoryList />
      </div>

      {/* ─── Edit Profile Modal ─── */}
      <Dialog open={showEditModal} onOpenChange={(open) => !open && setShowEditModal(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('profile.editProfile')}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleProfileSubmit(onProfileSubmit)} className="space-y-4">
            <div className="flex items-center gap-2 rounded-lg border bg-muted px-4 py-3">
              <span className="text-sm text-muted-foreground">@{user.username}</span>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>{t('common.firstName')}</Label>
                <Input {...registerProfile('firstName')} />
                {profileErrors.firstName && (
                  <p className="text-sm text-destructive">{profileErrors.firstName.message}</p>
                )}
              </div>
              <div className="space-y-2">
                <Label>{t('common.lastName')}</Label>
                <Input {...registerProfile('lastName')} />
                {profileErrors.lastName && (
                  <p className="text-sm text-destructive">{profileErrors.lastName.message}</p>
                )}
              </div>
            </div>
            <div className="space-y-2">
              <Label>{t('common.email')}</Label>
              <Input type="email" {...registerProfile('email')} />
              {profileErrors.email && (
                <p className="text-sm text-destructive">{profileErrors.email.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label>{t('common.phoneNumber')}</Label>
              <Input placeholder={t('common.phonePlaceholder')} {...registerProfile('phoneNumber')} />
              {profileErrors.phoneNumber && (
                <p className="text-sm text-destructive">{profileErrors.phoneNumber.message}</p>
              )}
            </div>
            <DialogFooter>
              <Button variant="outline" type="button" onClick={() => setShowEditModal(false)}>
                {t('common.cancel')}
              </Button>
              <Button type="submit" disabled={isUpdating}>
                {isUpdating ? t('common.saving') : t('common.save')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/* ─── PasswordField helper ───────────────────────────────────────────────── */
interface PasswordFieldProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label: string;
  show: boolean;
  onToggle: () => void;
  error?: string;
}

const PasswordField = ({ label, show, onToggle, error, ...inputProps }: PasswordFieldProps) => {
  const { t } = useTranslation();
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <div className="relative">
        <Input type={show ? 'text' : 'password'} {...inputProps} />
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="absolute top-0 ltr:right-0 rtl:left-0 h-full px-3"
          onClick={onToggle}
          aria-label={show ? t('common.hidePassword') : t('common.showPassword')}
        >
          {show ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        </Button>
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  );
};
