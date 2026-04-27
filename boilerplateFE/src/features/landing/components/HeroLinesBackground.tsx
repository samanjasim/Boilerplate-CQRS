/**
 * Hero background — flowing curved traces with a traveling glow pulse.
 *
 * Two layers per path:
 *  1. A faint static "guide" at ~10% opacity, always visible, so the network
 *     reads as a coherent constellation at constant density.
 *  2. An animated "pulse" — a small bright segment that travels continuously
 *     along the same path, glowing softly via an SVG filter. Each path has
 *     pathLength="100" so the dasharray cycle is normalized.
 *
 * Paths are Bezier curves (no sharp corners). Several intersect each other
 * at peripheral points to feel like a connected system, not parallel cracks.
 * The center reading area is masked out with a soft vignette so headline +
 * dashboard preview always stay primary.
 *
 * Respects prefers-reduced-motion via `.hero-pulse` rule (renders pulses as
 * full visible strokes with no animation).
 */
export function HeroLinesBackground() {
  return (
    <div
      aria-hidden
      className="pointer-events-none absolute inset-0 overflow-hidden"
      style={{ zIndex: 0 }}
    >
      <svg
        className="absolute inset-0 w-full h-full"
        viewBox="0 0 800 500"
        preserveAspectRatio="xMidYMid slice"
        style={{
          // Soft hole through the center so the reading area always breathes
          maskImage:
            'radial-gradient(ellipse 50% 46% at 50% 52%, transparent 30%, black 80%)',
          WebkitMaskImage:
            'radial-gradient(ellipse 50% 46% at 50% 52%, transparent 30%, black 80%)',
        }}
      >
        <defs>
          {/* Stroke gradient — copper to violet, soft */}
          <linearGradient id="hero-trace-grad" x1="0" x2="1" y1="0" y2="1">
            <stop offset="0%"  stopColor="var(--color-primary-700)" />
            <stop offset="55%" stopColor="var(--color-primary)" />
            <stop offset="100%" stopColor="var(--color-violet-500)" />
          </linearGradient>

          {/* Subtle glow — small inner highlight + gentle outer halo. Tuned softer
              than the previous version to read as polished trace, not crack. */}
          <filter id="hero-trace-glow" x="-30%" y="-30%" width="160%" height="160%">
            <feGaussianBlur stdDeviation="1.5" result="b1" />
            <feGaussianBlur stdDeviation="4"   result="b2" />
            <feMerge>
              <feMergeNode in="b2" />
              <feMergeNode in="b1" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        {/* Static guides — always visible at low opacity */}
        <g
          stroke="url(#hero-trace-grad)"
          strokeWidth="0.8"
          fill="none"
          strokeLinecap="round"
          strokeLinejoin="round"
          opacity="0.18"
        >
          {PATHS.map((p, i) => (
            <path key={`g-${i}`} d={p.d} />
          ))}
        </g>

        {/* Animated pulses — bright traveling segment along each path */}
        <g
          stroke="url(#hero-trace-grad)"
          strokeWidth="1.2"
          fill="none"
          strokeLinecap="round"
          strokeLinejoin="round"
          filter="url(#hero-trace-glow)"
        >
          {PATHS.map((p, i) => (
            <path
              key={`p-${i}`}
              d={p.d}
              pathLength={100}
              className="hero-pulse"
              style={{
                animationDelay: `-${p.delay}s`,
                ['--ht-dur' as string]: `${p.duration}s`,
              }}
            />
          ))}
        </g>
      </svg>
    </div>
  );
}

/** Eight Bezier-curved paths routed around the periphery with deliberate
 *  intersections at peripheral points. Durations and delays are spread so
 *  pulses are always at different positions — never all bunched. */
const PATHS: { d: string; delay: number; duration: number }[] = [
  // 1) Long top sweep — gentle dip then rise across the top
  { d: 'M 20 80 Q 200 40 380 100 T 760 80',                                   delay: 0,    duration: 11 },
  // 2) Diagonal flow upper-right → lower-left (crosses #1, #6 mid)
  { d: 'M 760 60 Q 600 220 380 260 Q 200 300 40 420',                         delay: 1.4,  duration: 13 },
  // 3) Long bottom sweep — mirrors #1, slightly different rhythm
  { d: 'M 20 460 Q 220 400 400 440 Q 580 480 780 420',                        delay: 2.8,  duration: 12 },
  // 4) Left ascender — meets #1 near top-left, #3 near bottom-left
  { d: 'M 80 60 Q 40 200 60 360 Q 80 460 160 460',                            delay: 3.6,  duration: 9  },
  // 5) Right ascender — meets #1 near top-right, #3 near bottom-right
  { d: 'M 740 80 Q 760 240 740 380 Q 720 440 660 460',                        delay: 4.2,  duration: 9.5},
  // 6) Cross-diagonal lower-left → upper-right (crosses #2)
  { d: 'M 80 380 Q 280 320 460 320 Q 620 320 760 240',                        delay: 5.4,  duration: 10 },
  // 7) Short interior arc top — accent
  { d: 'M 240 60 Q 340 30 440 70',                                            delay: 6.6,  duration: 7  },
  // 8) Short interior arc bottom — accent
  { d: 'M 320 460 Q 460 430 580 470',                                         delay: 7.4,  duration: 7.5},
];
