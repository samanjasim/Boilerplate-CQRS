import { useCallback, useMemo } from 'react';
import { useShallow } from 'zustand/react/shallow';
import { useAuthStore } from '@/stores';
import type { Permission } from '@/constants';

const EMPTY_PERMISSIONS: string[] = [];

export function usePermissions() {
  const permissions = useAuthStore(
    useShallow((state) => state.user?.permissions ?? EMPTY_PERMISSIONS)
  );

  const permissionSet = useMemo(() => new Set(permissions), [permissions]);

  const hasPermission = useCallback(
    (permission: Permission) => permissionSet.has(permission),
    [permissionSet]
  );

  const hasAnyPermission = useCallback(
    (perms: Permission[]) => perms.some((p) => permissionSet.has(p)),
    [permissionSet]
  );

  const hasAllPermissions = useCallback(
    (perms: Permission[]) => perms.every((p) => permissionSet.has(p)),
    [permissionSet]
  );

  return { permissions, hasPermission, hasAnyPermission, hasAllPermissions };
}
