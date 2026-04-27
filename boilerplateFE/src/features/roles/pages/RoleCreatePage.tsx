import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslation } from 'react-i18next';
import { ShieldCheck, Info } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader } from '@/components/common';
import { createRoleSchema, type CreateRoleFormData } from '@/lib/validation';
import { useCreateRole, useAllPermissions, useUpdateRolePermissions } from '../api';
import { PermissionMatrix } from '../components';
import { ROUTES } from '@/config';

export default function RoleCreatePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { mutateAsync: createRole, isPending: isCreating } = useCreateRole();
  const { mutate: updatePermissions, isPending: isAssigning } = useUpdateRolePermissions();
  const { data: allPermissions, isLoading: isLoadingPerms } = useAllPermissions();

  const [selectedPermissionIds, setSelectedPermissionIds] = useState<Set<string>>(new Set());

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateRoleFormData>({
    resolver: zodResolver(createRoleSchema),
    defaultValues: { name: '', description: '' },
  });

  const isSaving = isCreating || isAssigning;

  const onSubmit = async (data: CreateRoleFormData) => {
    try {
      const roleId = await createRole(data);

      if (selectedPermissionIds.size > 0) {
        updatePermissions(
          { id: roleId, data: { permissionIds: Array.from(selectedPermissionIds) } },
          { onSuccess: () => navigate(ROUTES.ROLES.getDetail(roleId)) }
        );
      } else {
        navigate(ROUTES.ROLES.getDetail(roleId));
      }
    } catch {
      // Error handled by mutation's onError / error interceptor
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('roles.newRole')}
        breadcrumbs={[
          { to: ROUTES.ROLES.LIST, label: t('roles.title') },
          { label: t('roles.create.title', 'Create role') },
        ]}
      />

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        {/* Step 1: Role Details */}
        <Card>
          <CardContent className="py-6">
            <div className="flex items-center gap-2 mb-5">
              <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-primary/10">
                <span className="text-xs font-bold text-primary">1</span>
              </div>
              <h3 className="text-lg font-semibold text-foreground">{t('roles.roleDetail')}</h3>
            </div>
            <div className="space-y-4 max-w-lg">
              <div className="space-y-2">
                <Label>{t('roles.name')}</Label>
                <Input
                  placeholder={t('roles.namePlaceholder')}
                  {...register('name')}
                />
                {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
              </div>
              <div className="space-y-2">
                <Label>{t('roles.description')}</Label>
                <Textarea
                  placeholder={t('roles.descriptionPlaceholder')}
                  rows={3}
                  {...register('description')}
                />
                {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Step 2: Assign Permissions */}
        <Card>
          <CardContent className="py-6">
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-2">
                <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-primary/10">
                  <span className="text-xs font-bold text-primary">2</span>
                </div>
                <h3 className="text-lg font-semibold text-foreground">{t('roles.assignPermissions')}</h3>
              </div>
              {selectedPermissionIds.size > 0 && (
                <div className="flex items-center gap-1.5 text-xs text-primary font-medium">
                  <ShieldCheck className="h-3.5 w-3.5" />
                  {t('roles.permissionsSelected', { count: selectedPermissionIds.size })}
                </div>
              )}
            </div>

            <div className="flex items-start gap-2 mb-4 rounded-lg bg-blue-50 dark:bg-blue-500/10 border border-blue-200 dark:border-blue-500/20 px-3 py-2.5">
              <Info className="h-4 w-4 shrink-0 mt-0.5 text-blue-600 dark:text-blue-400" />
              <p className="text-xs text-blue-700 dark:text-blue-300">
                <strong>{t('roles.permissionsInfoTitle')}</strong>{' '}
                {t('roles.permissionsInfoDesc')}
              </p>
            </div>

            {isLoadingPerms ? (
              <div className="flex justify-center py-8">
                <Spinner size="md" />
              </div>
            ) : allPermissions ? (
              <PermissionMatrix
                allPermissions={allPermissions}
                selectedIds={selectedPermissionIds}
                onChange={setSelectedPermissionIds}
              />
            ) : null}
          </CardContent>
        </Card>

        {/* Actions */}
        <div className="flex items-center gap-3">
          <Button type="submit" disabled={isSaving}>
            {isSaving ? t('common.loading') : t('common.create')}
          </Button>
          <Link to={ROUTES.ROLES.LIST}>
            <Button variant="outline" type="button">{t('common.cancel')}</Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
