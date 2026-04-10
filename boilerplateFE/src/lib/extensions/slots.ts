import type { ComponentType } from 'react';
import type { SlotMap, SlotId } from './slot-map';

/**
 * One entry registered against a named slot.
 *
 * `component` receives the props declared on the corresponding `SlotMap` key.
 * Modules typically use `lazy()` so the component code only loads when the
 * slot is actually rendered.
 */
export interface SlotEntry<S extends SlotId = SlotId> {
  /** Stable id for ordering, debugging, and React keying. */
  id: string;
  /** Owning module name (for debugging). */
  module: string;
  /** Sort order — lower runs first. */
  order: number;
  /** Optional human-readable label (e.g., for tab titles). */
  label?: () => string;
  /** Optional icon component for tabs/menus. */
  icon?: ComponentType<{ className?: string }>;
  /** Optional permission required to render this entry. */
  permission?: string;
  /** The component to render. Receives `SlotMap[S]` as props. */
  component: ComponentType<SlotMap[S]>;
}

/**
 * Internal storage. Entries are stored as `unknown` per-slot array because
 * `SlotEntry<S>` is invariant over `S` (via the `component` prop), so there
 * is no sound TypeScript type that can hold entries for different slot ids
 * in the same collection. We recover the correct `SlotEntry<S>` on read via
 * a cast — safe because the map key `S` narrows which entries can land in
 * which array.
 */
const registry = new Map<SlotId, unknown[]>();

/**
 * Register a component against a named slot. Called by module `index.ts`
 * files at app bootstrap (before React mounts).
 *
 * Calling `registerSlot` with the same `entry.id` replaces the prior entry —
 * this makes the registry safe for Vite HMR, where a module file edit causes
 * `register()` to run again.
 */
export function registerSlot<S extends SlotId>(slot: S, entry: SlotEntry<S>): void {
  const existing = (registry.get(slot) ?? []) as SlotEntry<S>[];
  const filtered = existing.filter((e) => e.id !== entry.id);
  filtered.push(entry);
  filtered.sort((a, b) => a.order - b.order);
  registry.set(slot, filtered);
}

/**
 * Read all entries currently registered against a slot.
 *
 * The cast from `unknown[]` to `SlotEntry<S>[]` is runtime-safe because the
 * map key `slot` narrows which entries can have been written to that bucket.
 * `registerSlot<S>` enforces that only `SlotEntry<S>` values enter the bucket
 * keyed by `S`, so this cast recovers the original type.
 */
export function getSlotEntries<S extends SlotId>(slot: S): SlotEntry<S>[] {
  return (registry.get(slot) ?? []) as SlotEntry<S>[];
}

/**
 * Returns true if at least one entry is currently registered for the given
 * slot. Useful for conditional UI: "show this tab button only if a module
 * has actually contributed to its content".
 *
 * Synchronous and reliable as long as `registerAllModules()` has run before
 * the calling component renders (which `main.tsx` guarantees).
 */
export function hasSlotEntries(slot: SlotId): boolean {
  return (registry.get(slot)?.length ?? 0) > 0;
}

/** Test/debug helper — clear every registered entry. Not used at runtime. */
export function clearSlotRegistry(): void {
  registry.clear();
}
