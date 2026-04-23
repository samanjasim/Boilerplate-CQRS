import { UserPlus, KeyRound, Shield, Bell, Building, Users, Monitor, Share2 } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

export const NOTIFICATION_ICONS: Record<string, LucideIcon> = {
  UserCreated: UserPlus,
  PasswordChanged: KeyRound,
  RoleChanged: Shield,
  UserInvited: Users,
  TenantCreated: Building,
  InvitationAccepted: UserPlus,
  LoginFromNewDevice: Monitor,
  ResourceShared: Share2,
  default: Bell,
};
