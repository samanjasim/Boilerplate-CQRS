/**
 * Slot map — typed contracts for every named extension point in the app.
 *
 * Each entry is `slot-id → propsType`. When a module registers a component
 * to a slot, TypeScript enforces that its props type matches what the host
 * page passes via `<Slot id="..." props={...} />`.
 *
 * To add a new slot:
 *   1. Add `'my-slot': { /* props *\/ }` here
 *   2. Render it in core: `<Slot id="my-slot" props={...} />`
 *   3. Modules register entries via `registerSlot('my-slot', { ... })`
 *
 * To add a new prop to an existing slot, update the type here — TypeScript
 * will surface every consumer that needs to be updated.
 */
export interface SlotMap {
  /** Tabs rendered inside the tenant detail page. */
  'tenant-detail-tabs': {
    tenantId: string;
    tenantName: string;
  };

  /** Toolbar buttons rendered inside the users list page header. */
  'users-list-toolbar': {
    onRefresh: () => void;
  };

  /** Cards rendered in the dashboard stats grid by modules. */
  'dashboard-cards': Record<string, never>;

  /** Timeline (comments + activity) embedded in entity detail pages. */
  'entity-detail-timeline': {
    entityType: string;
    entityId: string;
  };
}

export type SlotId = keyof SlotMap;
