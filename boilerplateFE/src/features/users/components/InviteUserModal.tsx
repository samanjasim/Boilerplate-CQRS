import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAssignableRoles } from '@/features/roles/api';
import { useInviteUser } from '@/features/auth/api';
import { useTenants } from '@/features/tenants/api';
import { useAuthStore } from '@/stores';
import type { Tenant } from '@/types';

const inviteUserSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Invalid email address'),
  roleId: z.string().optional(),
  tenantId: z.string().optional(),
});

type InviteUserFormData = z.infer<typeof inviteUserSchema>;

interface InviteUserModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function InviteUserModal({ open, onOpenChange }: InviteUserModalProps) {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const isPlatformAdmin = !user?.tenantId;

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors },
  } = useForm<InviteUserFormData>({
    resolver: zodResolver(inviteUserSchema),
    defaultValues: { email: '', roleId: '', tenantId: '' },
  });

  const selectedTenantId = watch('tenantId');

  // Platform admin: load tenants for the dropdown
  const { data: tenantsData } = useTenants(
    isPlatformAdmin && open ? { pageNumber: 1, pageSize: 100 } : undefined
  );
  const tenants: Tenant[] = tenantsData?.data ?? [];

  // Load assignable roles filtered by selected tenant
  const { data: assignableRoles } = useAssignableRoles(
    isPlatformAdmin ? (selectedTenantId || undefined) : undefined,
    { enabled: open }
  );
  const roles = assignableRoles ?? [];

  const { mutate: inviteUser, isPending } = useInviteUser();

  // Reset role when tenant changes
  useEffect(() => {
    setValue('roleId', '');
  }, [selectedTenantId, setValue]);

  const onSubmit = (data: InviteUserFormData) => {
    const payload: { email: string; roleId?: string; tenantId?: string } = {
      email: data.email,
    };
    if (data.roleId) payload.roleId = data.roleId;
    if (isPlatformAdmin && data.tenantId) payload.tenantId = data.tenantId;

    inviteUser(payload, {
      onSuccess: () => {
        reset();
        onOpenChange(false);
      },
    });
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) reset();
    onOpenChange(isOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>{t('invitations.inviteUser')}</DialogTitle>
          <DialogDescription>{t('invitations.inviteUserDesc')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="invite-email">{t('common.email')}</Label>
            <Input
              id="invite-email"
              type="email"
              placeholder={t('auth.enterEmail')}
              {...register('email')}
            />
            {errors.email && (
              <p className="text-sm text-destructive">{errors.email.message}</p>
            )}
          </div>

          {/* Tenant selector — platform admin only */}
          {isPlatformAdmin && (
            <div className="space-y-2">
              <Label>{t('invitations.tenant')}</Label>
              <Select onValueChange={(value) => setValue('tenantId', value === '__platform__' ? '' : value)}>
                <SelectTrigger>
                  <SelectValue placeholder={t('invitations.selectTenant')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__platform__">{t('invitations.platformLevel')}</SelectItem>
                  {tenants.map((tenant) => (
                    <SelectItem key={tenant.id} value={tenant.id}>
                      {tenant.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">{t('invitations.tenantHint')}</p>
            </div>
          )}

          <div className="space-y-2">
            <Label>{t('invitations.role')}</Label>
            <Select onValueChange={(value) => setValue('roleId', value === '__default__' ? '' : value)}>
              <SelectTrigger>
                <SelectValue placeholder={t('invitations.selectRole')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__default__">{t('invitations.useDefaultRole')}</SelectItem>
                {roles.map((role) => (
                  <SelectItem key={role.id} value={role.id}>
                    {role.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">{t('invitations.roleHint')}</p>
          </div>

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? t('common.loading') : t('invitations.sendInvite')}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
