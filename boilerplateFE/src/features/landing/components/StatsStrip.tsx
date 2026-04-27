import { useEffect, useRef, useState } from 'react';

interface Stat {
  target: number;
  suffix: string;
  label: string;
  caption: string;
  /** Sparkline path data — viewBox 0 0 100 30 */
  spark: string;
}

const STATS: Stat[] = [
  {
    target: 15,
    suffix: '',
    label: 'Backend features',
    caption: 'Tested across 200+ command handlers.',
    spark: 'M0,28 L15,24 L30,20 L45,22 L60,16 L75,12 L90,10 L100,4',
  },
  {
    target: 22,
    suffix: '',
    label: 'Frontend modules',
    caption: 'TypeScript-strict. Zero `as unknown as`.',
    spark: 'M0,26 L15,22 L30,24 L45,18 L60,14 L75,16 L90,8 L100,6',
  },
  {
    target: 3,
    suffix: '',
    label: 'Production clients',
    caption: '.NET 10 · React 19 · Flutter 3.',
    spark: 'M0,28 L20,28 L40,18 L60,18 L80,8 L100,8',
  },
  {
    target: 0,
    suffix: '',
    label: 'Hello-worlds',
    caption: 'Every feature is a real implementation.',
    spark: 'M0,28 L100,28',
  },
];

function useCountUp(target: number, duration = 1400) {
  const ref = useRef<HTMLDivElement | null>(null);
  const [value, setValue] = useState(0);
  const started = useRef(false);

  useEffect(() => {
    if (typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      setValue(target);
      started.current = true;
      return;
    }
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
              const eased = 1 - Math.pow(1 - t, 3);
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

function StatItem({ stat }: { stat: Stat }) {
  const { value, ref } = useCountUp(stat.target);
  return (
    <div ref={ref} className="px-4 py-2">
      <div className="flex items-baseline justify-between gap-3 mb-2">
        <div className="text-[44px] sm:text-[52px] font-extralight tracking-[-0.04em] leading-none font-display gradient-text">
          {value}
          {stat.suffix}
        </div>
        <svg viewBox="0 0 100 30" className="h-7 w-16 shrink-0" preserveAspectRatio="none">
          <defs>
            <linearGradient id={`spark-${stat.label}`} x1="0" x2="1" y1="0" y2="0">
              <stop offset="0%" stopColor="var(--color-primary-700)" />
              <stop offset="100%" stopColor="var(--color-violet-500)" />
            </linearGradient>
          </defs>
          {/* Static guide */}
          <path
            d={stat.spark}
            fill="none"
            stroke={`url(#spark-${stat.label})`}
            strokeWidth="1.4"
            strokeLinecap="round"
            strokeLinejoin="round"
            opacity="0.45"
          />
          {/* Animated shimmer — bright pulse traveling along the same path */}
          <path
            d={stat.spark}
            pathLength={100}
            fill="none"
            stroke={`url(#spark-${stat.label})`}
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="spark-shimmer"
          />
        </svg>
      </div>
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-foreground mb-1">
        {stat.label}
      </div>
      <div className="text-[11px] text-muted-foreground leading-[1.5]">{stat.caption}</div>
    </div>
  );
}

export function StatsStrip() {
  return (
    <section className="relative">
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-24 border-y border-border/30">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3 inline-flex items-center gap-2">
          <span className="pulse-dot" />
          Numbers that matter · live from the repo
        </div>
        <h2 className="text-[28px] sm:text-[34px] font-light tracking-[-0.025em] leading-[1.15] mb-3 font-display max-w-[720px]">
          Real surface area.
          <br />
          <em className="not-italic font-medium gradient-text">No padding.</em>
        </h2>
        <p className="text-[14px] leading-[1.6] max-w-[560px] mb-10 text-muted-foreground">
          Every count below is a real implementation in the repo — wired up, tested, and accessible
          through the same admin UI a tenant would use.
        </p>

        <div className="grid gap-y-8 gap-x-2 md:grid-cols-2 lg:grid-cols-4 surface-glass rounded-2xl p-4 sm:p-6 border border-border/40">
          {STATS.map((s, i) => (
            <div
              key={s.label}
              className={i < STATS.length - 1 ? 'lg:border-r border-border/30' : ''}
            >
              <StatItem stat={s} />
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
