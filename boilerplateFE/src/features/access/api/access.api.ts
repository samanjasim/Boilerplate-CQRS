import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { ResourceGrant, ResourceType, ResourceVisibility, GrantSubjectType, AccessLevel } from '../types';

export const accessApi = {
  list: (resourceType: ResourceType, resourceId: string): Promise<ResourceGrant[]> =>
    apiClient.get(API_ENDPOINTS.ACCESS.GRANTS(resourceType, resourceId)).then(r => r.data.data),

  grant: (
    resourceType: ResourceType,
    resourceId: string,
    body: { subjectType: GrantSubjectType; subjectId: string; level: AccessLevel },
  ) => apiClient.post(API_ENDPOINTS.ACCESS.GRANTS(resourceType, resourceId), body),

  revoke: (resourceType: ResourceType, resourceId: string, grantId: string) =>
    apiClient.delete(API_ENDPOINTS.ACCESS.GRANT(resourceType, resourceId, grantId)),

  setVisibility: (resourceType: ResourceType, resourceId: string, visibility: ResourceVisibility) =>
    apiClient.put(API_ENDPOINTS.ACCESS.VISIBILITY(resourceType, resourceId), { visibility }),

  transferOwnership: (resourceType: ResourceType, resourceId: string, newOwnerId: string) =>
    apiClient.post(API_ENDPOINTS.ACCESS.TRANSFER_OWNERSHIP(resourceType, resourceId), { newOwnerId }),
};
