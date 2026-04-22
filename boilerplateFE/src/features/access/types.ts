export type ResourceType = 'File' | 'AiAssistant';
export type ResourceVisibility = 'Private' | 'TenantWide' | 'Public';
export type GrantSubjectType = 'User' | 'Role';
export type AccessLevel = 'Viewer' | 'Editor' | 'Manager';
export type AssistantAccessMode = 'CallerPrincipal' | 'AssistantPrincipal';

export interface ResourceGrant {
  id: string;
  resourceType: ResourceType;
  resourceId: string;
  subjectType: GrantSubjectType;
  subjectId: string;
  subjectDisplayName: string | null;
  level: AccessLevel;
  grantedByUserId: string;
  grantedAt: string;
}
