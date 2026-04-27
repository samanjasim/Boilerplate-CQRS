import { useEffect, useRef, useState } from 'react';

interface ScrambleTextProps {
  text: string;
  /** Per-character settle interval in ms. Default 24. */
  step?: number;
  /** Total scramble duration before all settled, in ms. Default 1200. */
  duration?: number;
  /** Charset to scramble through. Default mixed alphanumeric + symbols. */
  chars?: string;
  /** Delay before scramble starts (ms). Default 0. */
  delay?: number;
  /** Optional className passed through. */
  className?: string;
  /** Render as block element (preserves line-height). Default `span`. */
  as?: 'span' | 'div';
}

const DEFAULT_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ0123456789!*+#@%$&';

/**
 * Renders text with a "decrypt" animation: each character scrambles through
 * random characters before settling on its final value. Plays once on mount.
 *
 * Respects prefers-reduced-motion (renders the final text immediately).
 */
export function ScrambleText({
  text,
  step = 24,
  duration = 1200,
  chars = DEFAULT_CHARS,
  delay = 0,
  className,
  as = 'span',
}: ScrambleTextProps) {
  const [output, setOutput] = useState(() =>
    typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
      ? text
      : text.replace(/[^\s]/g, () => (chars[Math.floor(Math.random() * chars.length)] ?? '*')),
  );
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const startRef = useRef<number>(0);

  useEffect(() => {
    if (typeof window === 'undefined') return;
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      setOutput(text);
      return;
    }

    const totalChars = text.length;

    const start = () => {
      startRef.current = performance.now();
      intervalRef.current = setInterval(() => {
        const elapsed = performance.now() - startRef.current;
        const settled = Math.min(totalChars, Math.floor(elapsed / step));
        if (elapsed >= duration || settled >= totalChars) {
          setOutput(text);
          if (intervalRef.current) clearInterval(intervalRef.current);
          intervalRef.current = null;
          return;
        }
        let next = '';
        for (let i = 0; i < totalChars; i++) {
          const ch = text[i];
          if (ch === ' ' || ch === '\n' || ch === ' ') {
            next += ch;
            continue;
          }
          if (i < settled) {
            next += ch;
          } else {
            next += (chars[Math.floor(Math.random() * chars.length)] ?? '*');
          }
        }
        setOutput(next);
      }, 30);
    };

    const t = setTimeout(start, delay);
    return () => {
      clearTimeout(t);
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [text, step, duration, chars, delay]);

  const Tag = as as 'span' | 'div';
  return <Tag className={className}>{output}</Tag>;
}
