import { useEffect, useRef, useState, type RefObject } from 'react';

interface UseCountUpOptions {
  /** Animation duration in ms. Default 1000. */
  duration?: number;
  /** When false, the animation is suppressed and the value stays 0. Default true. */
  active?: boolean;
  /** When true, animation fires the first time the returned ref enters the viewport. Default false. */
  observe?: boolean;
  /** IntersectionObserver threshold when `observe` is true. Default 0.4. */
  threshold?: number;
}

interface UseCountUpResult<T extends HTMLElement> {
  value: number;
  ref: RefObject<T | null>;
}

/**
 * Animates an integer from 0 → `target` with cubic-out easing.
 * Honors `prefers-reduced-motion`: when reduced, returns `target` immediately (or 0 if `active=false`).
 *
 * Three trigger modes:
 *  - default: fires on mount (and re-fires when `target` changes)
 *  - `active` flag: fires when `active` flips true (used for entrance choreographies)
 *  - `observe`: fires the first time the returned `ref` is at least `threshold` visible
 */
export function useCountUp<T extends HTMLElement = HTMLDivElement>(
  target: number,
  { duration = 1000, active = true, observe = false, threshold = 0.4 }: UseCountUpOptions = {},
): UseCountUpResult<T> {
  const ref = useRef<T | null>(null);
  const [reducedMotion] = useState(
    () =>
      typeof window !== 'undefined' &&
      window.matchMedia?.('(prefers-reduced-motion: reduce)').matches,
  );
  const [animated, setAnimated] = useState(0);

  useEffect(() => {
    if (!active || reducedMotion || target === 0) return;

    let frameId: number | undefined;
    let cancelled = false;

    const run = () => {
      const start = performance.now();
      const tick = (now: number) => {
        if (cancelled) return;
        const t = Math.min(1, (now - start) / duration);
        const eased = 1 - Math.pow(1 - t, 3);
        setAnimated(Math.round(target * eased));
        if (t < 1) frameId = requestAnimationFrame(tick);
      };
      frameId = requestAnimationFrame(tick);
    };

    if (!observe) {
      run();
      return () => {
        cancelled = true;
        if (frameId !== undefined) cancelAnimationFrame(frameId);
      };
    }

    const node = ref.current;
    if (!node) return;
    let started = false;
    const obs = new IntersectionObserver(
      (entries) => {
        for (const e of entries) {
          if (e.isIntersecting && !started) {
            started = true;
            run();
          }
        }
      },
      { threshold },
    );
    obs.observe(node);
    return () => {
      cancelled = true;
      obs.disconnect();
      if (frameId !== undefined) cancelAnimationFrame(frameId);
    };
  }, [target, duration, active, observe, threshold, reducedMotion]);

  return { value: active && reducedMotion ? target : animated, ref };
}
