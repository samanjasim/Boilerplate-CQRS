/**
 * Glowing-trace hero background.
 *
 * Eight paths routed around the perimeter of a virtual 800×500 viewport,
 * deliberately avoiding the central reading area. Each path draws itself
 * (stroke-dashoffset 100→0), holds visible while glowing, then erases
 * (0→-100) and restarts. Paths are staggered so something is always
 * drawing, holding, or fading — never all in sync, never silent.
 *
 * The whole `<g>` carries a multi-stop drop-shadow filter that produces a
 * soft inner highlight + wider outer halo. Stroke uses a copper→violet
 * gradient anchored to preset CSS vars so the active theme drives the look.
 *
 * Respects prefers-reduced-motion via `.hero-trace` rule in index.css
 * (renders all paths fully drawn and dimmed instead of cycling).
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
          // Cut a soft hole through the center so the headline + dashboard preview always read
          maskImage:
            'radial-gradient(ellipse 56% 50% at 50% 50%, transparent 35%, black 78%)',
          WebkitMaskImage:
            'radial-gradient(ellipse 56% 50% at 50% 50%, transparent 35%, black 78%)',
        }}
      >
        <defs>
          <linearGradient id="hero-trace-grad" x1="0" x2="1" y1="0" y2="1">
            <stop offset="0%" stopColor="var(--color-primary-700)" />
            <stop offset="55%" stopColor="var(--color-primary)" />
            <stop offset="100%" stopColor="var(--color-violet-500)" />
          </linearGradient>

          {/* Soft glow filter — combines a tight inner blur with a wider outer bloom */}
          <filter id="hero-trace-glow" x="-50%" y="-50%" width="200%" height="200%">
            <feGaussianBlur stdDeviation="2.4" result="blur1" />
            <feGaussianBlur stdDeviation="6"   result="blur2" />
            <feMerge>
              <feMergeNode in="blur2" />
              <feMergeNode in="blur1" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        <g
          stroke="url(#hero-trace-grad)"
          strokeWidth="1.5"
          fill="none"
          strokeLinecap="round"
          strokeLinejoin="round"
          filter="url(#hero-trace-glow)"
        >
          {PATHS.map((p, i) => (
            <path
              key={i}
              d={p.d}
              pathLength={100}
              className="hero-trace"
              style={{
                animationDelay: `${p.delay}s`,
                ['--ht-dur' as string]: `${p.duration}s`,
              }}
            />
          ))}
        </g>
      </svg>
    </div>
  );
}

const PATHS: { d: string; delay: number; duration: number }[] = [
  // Top backbone — long, slow
  { d: 'M 60 50 L 740 50',                                delay: 0,    duration: 13 },
  // Top-left L drop
  { d: 'M 40 60 L 40 200 L 130 200',                      delay: 1.2,  duration: 9  },
  // Top-right T-junction with branch
  { d: 'M 760 70 L 760 160 L 660 160 L 660 240',          delay: 2.0,  duration: 12 },
  // Middle-right diagonal connector
  { d: 'M 760 280 L 740 360 L 760 440',                   delay: 3.4,  duration: 10 },
  // Bottom backbone — slightly offset from top so they don't echo
  { d: 'M 80 460 L 720 460',                              delay: 4.6,  duration: 14 },
  // Bottom-left branch up
  { d: 'M 280 460 L 280 380 L 360 380',                   delay: 5.8,  duration: 9  },
  // Bottom-right L
  { d: 'M 540 460 L 740 460 L 740 400',                   delay: 6.6,  duration: 10 },
  // Left edge mid-vertical
  { d: 'M 40 240 L 40 380 L 130 380',                     delay: 7.4,  duration: 11 },
];
