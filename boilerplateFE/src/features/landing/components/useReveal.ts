import { useEffect, useRef, useState } from 'react';

/**
 * One-shot scroll reveal: returns a ref to attach to the element + a `revealed` flag.
 * The element flips to `data-revealed="true"` once it crosses `threshold` of viewport.
 * Used with the `.reveal-fade`, `.reveal-up`, or `.reveal-stagger` utility classes
 * defined in `index.css`.
 */
export function useReveal<T extends HTMLElement = HTMLDivElement>(
  threshold = 0.18,
): { ref: React.RefObject<T | null>; revealed: boolean } {
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

  return { ref, revealed };
}
