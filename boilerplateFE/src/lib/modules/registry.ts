import type {
  CoreNavGroupId,
  ModuleNavContext,
  ModuleNavGroup,
  ModuleNavGroupContribution,
  ModuleNavItem,
  ModuleNavItemContribution,
  ModuleRouteContribution,
  ModuleRouteRegion,
  WebModule,
  WebModuleContext,
} from './web-module';
import { registerCapability } from '@/lib/extensions/capabilities';
import { registerSlot, type SlotEntry } from '@/lib/extensions/slots';
import type { SlotId } from '@/lib/extensions/slot-map';

const registeredModuleIds = new Set<string>();
const routeContributions = new Map<string, ModuleRouteContribution>();
const navGroupContributions = new Map<string, ModuleNavGroupContribution>();
const navItemContributions = new Map<CoreNavGroupId, Map<string, ModuleNavItemContribution>>();

function ensureUnique(map: Map<string, unknown>, id: string, kind: string): void {
  if (map.has(id)) {
    throw new Error(`Duplicate web module ${kind} id '${id}'. Contribution ids must be unique.`);
  }
}

export function registerWebModules(modules: WebModule[]): void {
  registeredModuleIds.clear();
  routeContributions.clear();
  navGroupContributions.clear();
  navItemContributions.clear();

  for (const mod of modules) {
    if (registeredModuleIds.has(mod.id)) {
      throw new Error(`Duplicate web module id '${mod.id}'. Module ids must match modules.catalog.json keys.`);
    }

    registeredModuleIds.add(mod.id);

    const ctx: WebModuleContext = {
      registerRoute(contribution) {
        ensureUnique(routeContributions, contribution.id, 'route');
        routeContributions.set(contribution.id, contribution);
      },
      registerNavGroup(contribution) {
        ensureUnique(navGroupContributions, contribution.id, 'nav group');
        navGroupContributions.set(contribution.id, contribution);
      },
      registerNavItem(groupId, contribution) {
        let bucket = navItemContributions.get(groupId);
        if (!bucket) {
          bucket = new Map();
          navItemContributions.set(groupId, bucket);
        }
        ensureUnique(bucket, contribution.id, `nav item (${groupId})`);
        bucket.set(contribution.id, contribution);
      },
      registerSlot<S extends SlotId>(slot: S, entry: SlotEntry<S>) {
        registerSlot(slot, entry);
      },
      registerCapability(key, implementation) {
        registerCapability(key, implementation);
      },
    };

    mod.register(ctx);
  }
}

export function getModuleRoutes(region: ModuleRouteRegion) {
  return [...routeContributions.values()]
    .filter((contribution) => contribution.region === region)
    .sort((a, b) => (a.order ?? 100) - (b.order ?? 100))
    .map((contribution) => contribution.route);
}

export function getModuleNavGroups(ctx: ModuleNavContext): ModuleNavGroup[] {
  return [...navGroupContributions.values()]
    .map((contribution): ModuleNavGroup | undefined => {
      const body = contribution.build(ctx);
      if (!body || body.items.length === 0) return undefined;
      return {
        id: contribution.id,
        ...(contribution.order === undefined ? {} : { order: contribution.order }),
        ...body,
      };
    })
    .filter((group): group is ModuleNavGroup => Boolean(group && group.items.length > 0))
    .sort((a, b) => (a.order ?? 100) - (b.order ?? 100));
}

export function getModuleNavItems(groupId: CoreNavGroupId, ctx: ModuleNavContext): ModuleNavItem[] {
  const bucket = navItemContributions.get(groupId);
  if (!bucket) return [];
  return [...bucket.values()]
    .map((contribution) => {
      const item = contribution.build(ctx);
      return item ? { item, order: contribution.order ?? 100 } : undefined;
    })
    .filter((entry): entry is { item: ModuleNavItem; order: number } => Boolean(entry))
    .sort((a, b) => a.order - b.order)
    .map((entry) => entry.item);
}

export function getRegisteredModuleIds(): string[] {
  return [...registeredModuleIds];
}
