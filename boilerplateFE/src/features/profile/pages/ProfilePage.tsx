import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import {
  Mail,
  Phone,
  Clock,
  Shield,
  Pencil,
  Eye,
  EyeOff,
  Building,
  CheckCircle,
  XCircle,
} from 'lucide-react';
import { format } from 'date-fns';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader, InfoField } from '@/components/common';
import { toast } from 'sonner';
import { useAuthStore, selectUser } from '@/stores';
import { useChangePassword } from '@/features/auth/api';
import { useUpdateUser } from '@/features/users/api';
import { TwoFactorSetup } from '../components/TwoFactorSetup';
import { SessionsList } from '../components/SessionsList';
import { LoginHistoryList } from '../components/LoginHistoryList';
import { NotificationPreferences } from '../components/NotificationPreferences';
import { queryKeys } from '@/lib/query/keys';
import {
  changePasswordSchema,
  type ChangePasswordFormData,
  updateUserSchema,
  type UpdateUserFormData,
} from '@/lib/validation';

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
    defaultValues: {
      currentPassword: '',
      newPassword: '',
      confirmNewPassword: '',
    },
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
    const payload = {
      ...data,
      phoneNumber: data.phoneNumber || null,
    };
    updateUser(
      { id: user.id, data: payload },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() });
          toast.success(t('profile.profileUpdated'));
          setShowEditModal(false);
        },
      }
    );
  };

  return (
    <div className="space-y-6">
      <PageHeader title={t('profile.title')} />

      {/* Profile Info Card */}
      <Card>
        <CardContent className="py-6">
          <div className="flex items-start gap-4 mb-6">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-lg font-bold text-primary">
              {user.firstName.charAt(0)}
              {user.lastName.charAt(0)}
            </div>
            <div className="min-w-0 flex-1">
              <h2 className="text-xl font-bold text-foreground">
                {user.firstName} {user.lastName}
              </h2>
              <p className="text-muted-foreground">@{user.username}</p>
            </div>
            <Button variant="outline" size="sm" onClick={() => setShowEditModal(true)}>
              <Pencil className="h-4 w-4" />
              {t('profile.editProfile')}
            </Button>
          </div>

          <div className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
            <InfoField label={t('common.email')}>
              <div className="flex items-center gap-2">
                <Mail className="h-4 w-4 text-muted-foreground" />
                <span>{user.email}</span>
              </div>
            </InfoField>
            {user.phoneNumber && (
              <InfoField label={t('common.phone')}>
                <div className="flex items-center gap-2">
                  <Phone className="h-4 w-4 text-muted-foreground" />
                  <span>{user.phoneNumber}</span>
                </div>
              </InfoField>
            )}
            <InfoField label={t('auth.username')}>
              <span>@{user.username}</span>
            </InfoField>
            {user.tenantName && (
              <InfoField label={t('profile.tenant')}>
                <div className="flex items-center gap-2">
                  <Building className="h-4 w-4 text-muted-foreground" />
                  <span>{user.tenantName}</span>
                </div>
              </InfoField>
            )}
          </div>

          {user.roles && user.roles.length > 0 && (
            <div className="mt-4 pt-4 border-t">
              <div className="flex items-center gap-2 mb-2">
                <Shield className="h-4 w-4 text-primary" />
                <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {t('users.userRoles')}
                </span>
              </div>
              <div className="flex flex-wrap gap-2">
                {user.roles.map((role) => (
                  <Badge key={role} variant="secondary">
                    {role}
                  </Badge>
                ))}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Change Password Card */}
      <Card>
        <CardContent className="py-6">
          <h3 className="text-lg font-semibold text-foreground mb-4">
            {t('profile.changePassword')}
          </h3>
          <form onSubmit={handlePasswordSubmit(onPasswordSubmit)} className="space-y-4 max-w-md">
            <div className="space-y-2">
              <Label>{t('profile.currentPassword')}</Label>
              <div className="relative">
                <Input
                  type={showCurrentPassword ? 'text' : 'password'}
                  {...registerPassword('currentPassword')}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="absolute top-0 ltr:right-0 rtl:left-0 h-full px-3"
                  onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                  aria-label={showCurrentPassword ? t('common.hidePassword') : t('common.showPassword')}
                >
                  {showCurrentPassword ? (
                    <EyeOff className="h-4 w-4" />
                  ) : (
                    <Eye className="h-4 w-4" />
                  )}
                </Button>
              </div>
              {passwordErrors.currentPassword && (
                <p className="text-sm text-destructive">{passwordErrors.currentPassword.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label>{t('profile.newPassword')}</Label>
              <div className="relative">
                <Input
                  type={showNewPassword ? 'text' : 'password'}
                  {...registerPassword('newPassword')}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="absolute top-0 ltr:right-0 rtl:left-0 h-full px-3"
                  onClick={() => setShowNewPassword(!showNewPassword)}
                  aria-label={showNewPassword ? t('common.hidePassword') : t('common.showPassword')}
                >
                  {showNewPassword ? (
                    <EyeOff className="h-4 w-4" />
                  ) : (
                    <Eye className="h-4 w-4" />
                  )}
                </Button>
              </div>
              {passwordErrors.newPassword && (
                <p className="text-sm text-destructive">{passwordErrors.newPassword.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label>{t('profile.confirmNewPassword')}</Label>
              <div className="relative">
                <Input
                  type={showConfirmPassword ? 'text' : 'password'}
                  {...registerPassword('confirmNewPassword')}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="absolute top-0 ltr:right-0 rtl:left-0 h-full px-3"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  aria-label={showConfirmPassword ? t('common.hidePassword') : t('common.showPassword')}
                >
                  {showConfirmPassword ? (
                    <EyeOff className="h-4 w-4" />
                  ) : (
                    <Eye className="h-4 w-4" />
                  )}
                </Button>
              </div>
              {passwordErrors.confirmNewPassword && (
                <p className="text-sm text-destructive">
                  {passwordErrors.confirmNewPassword.message}
                </p>
              )}
            </div>
            <Button type="submit" disabled={isChangingPassword}>
              {isChangingPassword ? t('common.saving') : t('profile.changePassword')}
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Notification Preferences */}
      <NotificationPreferences />

      {/* Two-Factor Authentication Card */}
      <TwoFactorSetup user={user} />

      {/* Active Sessions Card */}
      <SessionsList />

      {/* Login History Card */}
      <LoginHistoryList />

      {/* Account Info Card */}
      <Card>
        <CardContent className="py-6">
          <h3 className="text-lg font-semibold text-foreground mb-4">
            {t('profile.accountInfo')}
          </h3>
          <div className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
            <InfoField label={t('common.email')}>
              <div className="flex items-center gap-2">
                {user.emailConfirmed ? (
                  <>
                    <CheckCircle className="h-4 w-4 text-green-500" />
                    <Badge variant="default">{t('profile.emailVerified')}</Badge>
                  </>
                ) : (
                  <>
                    <XCircle className="h-4 w-4 text-destructive" />
                    <Badge variant="destructive">{t('profile.emailNotVerified')}</Badge>
                  </>
                )}
              </div>
            </InfoField>
            {user.lastLoginAt && (
              <InfoField label={t('profile.lastLogin')}>
                <div className="flex items-center gap-2">
                  <Clock className="h-4 w-4 text-muted-foreground" />
                  <span>{format(new Date(user.lastLoginAt), 'MMM d, yyyy HH:mm')}</span>
                </div>
              </InfoField>
            )}
            {user.createdAt && (
              <InfoField label={t('profile.memberSince')}>
                <span>{format(new Date(user.createdAt), 'MMMM d, yyyy')}</span>
              </InfoField>
            )}
            <InfoField label={t('common.status')}>
              <Badge variant={user.status === 'Active' ? 'default' : 'destructive'}>
                {user.status || t('common.active')}
              </Badge>
            </InfoField>
          </div>
        </CardContent>
      </Card>

      {/* Edit Profile Modal */}
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
