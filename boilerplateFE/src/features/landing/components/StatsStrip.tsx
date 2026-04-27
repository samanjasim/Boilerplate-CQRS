import { useEffect, useRef, useState } from 'react';

const STATS: { target: number; suffix: string; label: string }[] = [
  { target: 15, suffix: '', label: 'backend features' },
  { target: 22, suffix: '', label: 'frontend modules' },
  { target: 3, suffix: '', label: 'production clients' },
  { target: 0, suffix: '', label: 'hello-worlds' },
];

/** Count up from 0 to target over `duration` ms. Starts when the element enters the viewport. */
function useCountUp(target: number, duration = 1200): { value: number; ref: React.RefObject<HTMLDivElement | null> } {
  const ref = useRef<HTMLDivElement | null>(null);
  const [value, setValue] = useState(0);
  const started = useRef(false);

  useEffect(() => {
    if (target === 0) {
      setValue(0);
      return;
    }
    const node = ref.current;
    if (!node) return;
    const obs = new IntersectionObserver(
      (entries) => {
        for (const e of entries) {
          if (e.isIntersecting && !started.current) {
            started.current = true;
            const start = performance.now();
            const tick = (now: number) => {
              const t = Math.min(1, (now - start) / duration);
              const eased = 1 - Math.pow(1 - t, 3); // easeOutCubic
              setValue(Math.round(target * eased));
              if (t < 1) requestAnimationFrame(tick);
            };
            requestAnimationFrame(tick);
          }
        }
      },
      { threshold: 0.4 },
    );
    obs.observe(node);
    return () => obs.disconnect();
  }, [target, duration]);

  return { value, ref };
}

function StatItem({ target, suffix, label }: { target: number; suffix: string; label: string }) {
  const { value, ref } = useCountUp(target);
  return (
    <div ref={ref}>
      <div className="text-[44px] sm:text-[52px] font-extralight tracking-[-0.04em] leading-none font-display gradient-text font-feature-settings">
        {value}
        {suffix}
      </div>
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] mt-3 text-muted-foreground">
        {label}
      </div>
    </div>
  );
}

export function StatsStrip() {
  return (
    <div className="border-y border-border/30 surface-glass relative">
      <div className="mx-auto max-w-6xl px-7 py-12 grid grid-cols-2 md:grid-cols-4 gap-y-8">
        {STATS.map((s, i) => (
          <div
            key={s.label}
            className={`px-4 ${i < STATS.length - 1 ? 'md:border-r border-border/30' : ''}`}
          >
            <StatItem target={s.target} suffix={s.suffix} label={s.label} />
          </div>
        ))}
      </div>
    </div>
  );
}
