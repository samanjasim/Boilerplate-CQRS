import { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader } from '@/components/common';
import { updateRoleSchema, type UpdateRoleFormData } from '@/lib/validation';
import { useRole, useUpdateRole, useAllPermissions, useUpdateRolePermissions } from '../api';
import { PermissionMatrix } from '../components';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';

export default function RoleEditPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { hasPermission } = usePermissions();

  const { data: role, isLoading: isLoadingRole } = useRole(id!);
  const { data: allPermissions, isLoading: isLoadingPerms } = useAllPermissions();
  const { mutate: updateRole, isPending: isUpdatingRole } = useUpdateRole();
  const { mutate: updatePermissions, isPending: isUpdatingPerms } = useUpdateRolePermissions();

  const canUpdate = hasPermission(PERMISSIONS.Roles.Update);
  const canManagePermissions = hasPermission(PERMISSIONS.Roles.ManagePermissions);

  const [selectedPermissionIds, setSelectedPermissionIds] = useState<Set<string>>(new Set());

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<UpdateRoleFormData>({
    resolver: zodResolver(updateRoleSchema),
  });

  // Initialize form + permission selection when role loads
  useEffect(() => {
    if (role) {
      reset({ name: role.name, description: role.description || '' });
      setSelectedPermissionIds(new Set(role.permissions?.map((p) => p.id) ?? []));
    }
  }, [role, reset]);

  const initialPermIds = useMemo(
    () => new Set(role?.permissions?.map((p) => p.id) ?? []),
    [role]
  );

  const permissionsChanged = useMemo(() => {
    if (selectedPermissionIds.size !== initialPermIds.size) return true;
    for (const id of selectedPermissionIds) {
      if (!initialPermIds.has(id)) return true;
    }
    return false;
  }, [selectedPermissionIds, initialPermIds]);

  const hasChanges = isDirty || permissionsChanged;
  const isSaving = isUpdatingRole || isUpdatingPerms;

  if (isLoadingRole || isLoadingPerms) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!role) {
    return <div className="text-muted-foreground">{t('common.noResults')}</div>;
  }

  const onSubmit = async (data: UpdateRoleFormData) => {
    const promises: Promise<void>[] = [];

    if (isDirty && canUpdate) {
      promises.push(
        new Promise((resolve, reject) =>
          updateRole({ id: role.id, data }, { onSuccess: () => resolve(), onError: reject })
        )
      );
    }

    if (permissionsChanged && canManagePermissions) {
      promises.push(
        new Promise((resolve, reject) =>
          updatePermissions(
            { id: role.id, data: { permissionIds: Array.from(selectedPermissionIds) } },
            { onSuccess: () => resolve(), onError: reject }
          )
        )
      );
    }

    if (promises.length > 0) {
      await Promise.all(promises);
    }
    navigate(ROUTES.ROLES.getDetail(role.id));
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Edit: ${role.name}`}
        backTo={ROUTES.ROLES.getDetail(role.id)}
        backLabel={t('roles.backToRoles')}
      />

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        {/* Role Details */}
        {canUpdate && (
          <Card>
            <CardContent className="py-6">
              <h3 className="mb-4 text-lg font-semibold text-foreground">{t('roles.roleDetail')}</h3>
              <div className="space-y-4 max-w-lg">
                <div className="space-y-2">
                  <Label>{t('roles.name')}</Label>
                  <Input
                    placeholder="e.g. Admin"
                    disabled={role.isSystemRole}
                    {...register('name')}
                  />
                  {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
                </div>
                <div className="space-y-2">
                  <Label>{t('roles.description')}</Label>
                  <Textarea
                    placeholder="Describe the role's purpose..."
                    rows={3}
                    disabled={role.isSystemRole}
                    {...register('description')}
                  />
                  {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
                </div>
              </div>
            </CardContent>
          </Card>
        )}

        {/* Permission Matrix */}
        {canManagePermissions && allPermissions && (
          <Card>
            <CardContent className="py-6">
              <h3 className="mb-4 text-lg font-semibold text-foreground">{t('roles.rolePermissions')}</h3>
              <PermissionMatrix
                allPermissions={allPermissions}
                selectedIds={selectedPermissionIds}
                onChange={setSelectedPermissionIds}
                disabled={role.isSystemRole}
              />
            </CardContent>
          </Card>
        )}

        {/* Actions */}
        <div className="flex items-center gap-3">
          <Button type="submit" disabled={isSaving || !hasChanges || role.isSystemRole}>
            {isSaving ? t('common.saving') : t('common.save')}
          </Button>
          <Link to={ROUTES.ROLES.getDetail(role.id)}>
            <Button variant="outline">{t('common.cancel')}</Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
