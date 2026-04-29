import type { ComponentType } from 'react';
import type { RouteObject } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';
import type { TFunction } from 'i18next';
import type { SlotEntry, SlotId } from '@/lib/extensions';

export type ModuleRouteRegion = 'public' | 'protected';

export interface ModuleRouteContribution {
  id: string;
  region: ModuleRouteRegion;
  route: RouteObject;
  order?: number;
}

export interface ModuleNavItem {
  label: string;
  icon: LucideIcon;
  path: string;
  end?: boolean;
  badge?: number;
  Badge?: ComponentType;
}

export interface ModuleNavGroup {
  id: string;
  label?: string;
  order?: number;
  items: ModuleNavItem[];
}

export type ModuleNavGroupBody = Omit<ModuleNavGroup, 'id' | 'order'>;

export interface ModuleNavContext {
  t: TFunction;
  hasPermission(permission: string): boolean;
  tenantScoped: boolean;
  isFeatureEnabled(key: string): boolean;
}

export interface ModuleNavGroupContribution {
  id: string;
  order?: number;
  build(ctx: ModuleNavContext): ModuleNavGroupBody | null | undefined;
}

export type CoreNavGroupId = 'top' | 'people' | 'content' | 'platform';

export interface ModuleNavItemContribution {
  id: string;
  order?: number;
  build(ctx: ModuleNavContext): ModuleNavItem | null | undefined;
}

export interface WebModuleContext {
  registerRoute(contribution: ModuleRouteContribution): void;
  registerNavGroup(contribution: ModuleNavGroupContribution): void;
  registerNavItem(groupId: CoreNavGroupId, contribution: ModuleNavItemContribution): void;
  registerSlot<S extends SlotId>(slot: S, entry: SlotEntry<S>): void;
  registerCapability<F extends (...args: never[]) => unknown>(key: string, implementation: F): void;
}

export interface WebModule {
  id: string;
  register(ctx: WebModuleContext): void;
}
