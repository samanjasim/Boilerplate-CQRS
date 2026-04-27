import { useEffect, useRef, useState } from 'react';
import type { RefObject } from 'react';

/**
 * One-shot scroll reveal: returns a tuple `[ref, revealed]` to attach to an element.
 * The element flips to `data-revealed="true"` once it crosses `threshold` of viewport.
 * Used with the `.reveal-fade`, `.reveal-up`, `.reveal-stagger`, or `.reveal-snap`
 * utility classes defined in `index.css`.
 *
 * Returns a tuple (rather than an object) so consumers always destructure into
 * uniquely-named locals — avoids React 19's `react-hooks/refs` rule which flags
 * any property access named `.ref` during render.
 */
export function useReveal<T extends HTMLElement = HTMLDivElement>(
  threshold = 0.18,
): readonly [RefObject<T | null>, boolean] {
  const ref = useRef<T | null>(null);
  const [revealed, setRevealed] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    if (revealed) return;
    const obs = new IntersectionObserver(
      (entries) => {
        for (const e of entries) {
          if (e.isIntersecting) {
            setRevealed(true);
            obs.disconnect();
            break;
          }
        }
      },
      { threshold, rootMargin: '0px 0px -8% 0px' },
    );
    obs.observe(node);
    return () => obs.disconnect();
  }, [revealed, threshold]);

  return [ref, revealed] as const;
}
