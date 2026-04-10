import { Suspense } from 'react';
import { usePermissions } from '@/hooks/usePermissions';
import type { Permission } from '@/constants';
import { getSlotEntries } from './slots';
import type { SlotMap, SlotId } from './slot-map';

interface SlotProps<S extends SlotId> {
  id: S;
  props: SlotMap[S];
  /** Rendered when no entries are registered (or all are permission-gated out). */
  fallback?: React.ReactNode;
}

/**
 * Render every component registered against the given slot, in order.
 *
 * Permission gating: entries with a `permission` field are filtered against
 * the current user's permissions before rendering. Lazy components are
 * wrapped in `<Suspense fallback={null}/>` so the page never blocks on
 * module code that's still loading.
 *
 * Core pages render `<Slot id="..." props={...} />` and have zero knowledge
 * of which modules contribute to the slot. Modules register their entries
 * in their `index.ts`.
 */
export function Slot<S extends SlotId>({ id, props, fallback }: SlotProps<S>) {
  const { hasPermission } = usePermissions();
  const entries = getSlotEntries(id);
  const visible = entries.filter((e) => !e.permission || hasPermission(e.permission as Permission));

  if (visible.length === 0) return <>{fallback ?? null}</>;

  return (
    <>
      {visible.map((entry) => {
        const Component = entry.component;
        return (
          <Suspense key={entry.id} fallback={null}>
            <Component {...props} />
          </Suspense>
        );
      })}
    </>
  );
}
