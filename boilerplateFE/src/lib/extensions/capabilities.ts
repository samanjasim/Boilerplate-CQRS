/**
 * Capability registry — runtime hooks/services that core code may consume
 * but whose implementation lives in a module.
 *
 * Use this when a SINGLE module is the natural provider for a capability
 * (vs. slots, which accept MULTIPLE visual contributions to a UI region).
 *
 * ── PATTERN ──
 *   1. Declare the capability shape in a module's `index.ts` (function sig).
 *   2. Modules call `registerCapability('myCap', impl)` from their `register()`
 *      function during app bootstrap.
 *   3. Core consumes via `getCapability('myCap')` — returns `undefined` if
 *      no module provided it, so callers must handle the missing case.
 *
 * ── REALISTIC EXAMPLE (future) ──
 *   A hypothetical `Starter.Module.Payments` module could expose a
 *   `processPayment` capability:
 *
 *     // features/payments/index.ts
 *     import { registerCapability } from '@/lib/extensions';
 *     async function processPayment(amount: number, token: string) {
 *       // call the payment gateway
 *     }
 *     export const paymentsModule = {
 *       name: 'payments',
 *       register() {
 *         registerCapability('processPayment', processPayment);
 *       },
 *     };
 *
 *     // features/checkout/hooks/useCheckout.ts
 *     import { getCapability } from '@/lib/extensions';
 *     export function useCheckout() {
 *       const pay = getCapability<(amount: number, token: string) => Promise<void>>(
 *         'processPayment',
 *       );
 *       if (!pay) {
 *         // Payments module isn't installed — fall back to "contact sales"
 *         return { disabled: true, reason: 'Payments not installed' };
 *       }
 *       return { disabled: false, submit: pay };
 *     }
 *
 *   This keeps the checkout page in core (it's generic), but the actual
 *   payment-processing code lives inside the removable module. Core has zero
 *   imports from `@/features/payments/*`.
 *
 * ── WHEN TO PICK THIS OVER A SLOT ──
 *   - Slot: "render zero-or-more components into this region" (tabs, toolbars,
 *     dashboard widgets).
 *   - Capability: "call this function when X happens" (process payment, emit
 *     analytics, evaluate a feature flag).
 */

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyFn = (...args: any[]) => any;

/**
 * Map of capability key → implementation. Loosely typed so each entry can
 * have its own signature; the typed accessors below enforce per-call safety.
 */
const capabilities = new Map<string, AnyFn>();

export function registerCapability<F extends AnyFn>(key: string, impl: F): void {
  capabilities.set(key, impl);
}

export function getCapability<F extends AnyFn>(key: string): F | undefined {
  return capabilities.get(key) as F | undefined;
}

/** Test/debug helper. */
export function clearCapabilityRegistry(): void {
  capabilities.clear();
}
