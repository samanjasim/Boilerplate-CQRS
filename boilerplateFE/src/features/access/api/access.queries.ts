import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import i18n from '@/i18n';
import { accessApi } from './access.api';
import type { ResourceType, ResourceVisibility, GrantSubjectType, AccessLevel } from '../types';

const qk = {
  grants: (t: ResourceType, id: string) => ['access', 'grants', t, id] as const,
};

export function useResourceGrants(resourceType: ResourceType, resourceId: string) {
  return useQuery({
    queryKey: qk.grants(resourceType, resourceId),
    queryFn: () => accessApi.list(resourceType, resourceId),
    enabled: !!resourceId,
  });
}

export function useGrantResourceAccess(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { subjectType: GrantSubjectType; subjectId: string; level: AccessLevel }) =>
      accessApi.grant(resourceType, resourceId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.grants(resourceType, resourceId) });
      toast.success(i18n.t('access.grantAdded'));
    },
  });
}

export function useRevokeResourceGrant(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (grantId: string) => accessApi.revoke(resourceType, resourceId, grantId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.grants(resourceType, resourceId) });
      toast.success(i18n.t('access.grantRevoked'));
    },
  });
}

export function useSetResourceVisibility(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (visibility: ResourceVisibility) =>
      accessApi.setVisibility(resourceType, resourceId, visibility),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['files'] });
      qc.invalidateQueries({ queryKey: qk.grants(resourceType, resourceId) });
      toast.success(i18n.t('access.visibilityUpdated'));
    },
  });
}

export function useTransferResourceOwnership(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (newOwnerId: string) => accessApi.transferOwnership(resourceType, resourceId, newOwnerId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['files'] });
      toast.success(i18n.t('access.ownershipTransferred'));
    },
  });
}
