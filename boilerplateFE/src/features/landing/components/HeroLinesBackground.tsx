/**
 * Hero background — three-layer composition.
 *
 *   1. Atmospheric backdrop: 3 long sweeping curves at very low opacity.
 *      Stationary, no pulses. Suggests deep network behind the foreground.
 *   2. Network mid-layer: 10 curved traces with always-visible guides plus a
 *      bright pulse traveling along each. Two gradient stroke directions
 *      (copper→violet and violet→copper) for variation; pulse length varies
 *      per path. Several paths converge at peripheral intersection points.
 *   3. Hub markers: 5 small soft-glowing nodes at chosen intersections,
 *      pulsing on independent slow opacity waves. Adds rhythm without dots
 *      cluttering the line geometry.
 *
 * The whole composition drifts subtly (translate + scale ≤ 1.2%) on a 42s
 * loop — the network breathes. A center-vignette mask hides the reading
 * area; an outer-ring fade hides the viewport edges so paths terminate
 * naturally instead of cutting off abruptly.
 *
 * Respects prefers-reduced-motion (renders all layers static).
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
          // Compound mask: center hole (reading area) + outer fade (viewport edges)
          maskImage:
            'radial-gradient(ellipse 50% 46% at 50% 52%, transparent 28%, black 60%, black 78%, transparent 98%)',
          WebkitMaskImage:
            'radial-gradient(ellipse 50% 46% at 50% 52%, transparent 28%, black 60%, black 78%, transparent 98%)',
        }}
      >
        <defs>
          {/* Forward gradient: copper-700 → copper → violet-500 */}
          <linearGradient id="trace-fwd" x1="0" x2="1" y1="0" y2="1">
            <stop offset="0%"  stopColor="var(--color-primary-700)" />
            <stop offset="55%" stopColor="var(--color-primary)" />
            <stop offset="100%" stopColor="var(--color-violet-500)" />
          </linearGradient>
          {/* Reverse gradient: violet-500 → copper → copper-700 (for variation) */}
          <linearGradient id="trace-rev" x1="0" x2="1" y1="0" y2="1">
            <stop offset="0%"   stopColor="var(--color-violet-500)" />
            <stop offset="45%"  stopColor="var(--color-primary)" />
            <stop offset="100%" stopColor="var(--color-primary-700)" />
          </linearGradient>

          {/* Soft trace glow */}
          <filter id="trace-glow" x="-30%" y="-30%" width="160%" height="160%">
            <feGaussianBlur stdDeviation="1.4" result="b1" />
            <feGaussianBlur stdDeviation="3.6" result="b2" />
            <feMerge>
              <feMergeNode in="b2" />
              <feMergeNode in="b1" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>

          {/* Wider, softer hub glow */}
          <filter id="hub-glow" x="-100%" y="-100%" width="300%" height="300%">
            <feGaussianBlur stdDeviation="2.5" result="hb1" />
            <feGaussianBlur stdDeviation="6"   result="hb2" />
            <feMerge>
              <feMergeNode in="hb2" />
              <feMergeNode in="hb1" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        <g className="hero-network-breathe">
          {/* ── Layer 1 — atmospheric backdrop ── */}
          <g
            stroke="url(#trace-fwd)"
            strokeWidth="0.7"
            fill="none"
            strokeLinecap="round"
            opacity="0.10"
          >
            {ATMOSPHERIC.map((d, i) => (
              <path key={`a-${i}`} d={d} />
            ))}
          </g>

          {/* ── Layer 2a — mid-network static guides ── */}
          <g
            strokeWidth="0.85"
            fill="none"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            {NETWORK.map((p, i) => (
              <path
                key={`g-${i}`}
                d={p.d}
                stroke={p.dir === 'fwd' ? 'url(#trace-fwd)' : 'url(#trace-rev)'}
                opacity={p.guideOpacity}
              />
            ))}
          </g>

          {/* ── Layer 2b — mid-network animated pulses ── */}
          <g
            strokeWidth="1.2"
            fill="none"
            strokeLinecap="round"
            strokeLinejoin="round"
            filter="url(#trace-glow)"
          >
            {NETWORK.map((p, i) => (
              <path
                key={`p-${i}`}
                d={p.d}
                pathLength={100}
                stroke={p.dir === 'fwd' ? 'url(#trace-fwd)' : 'url(#trace-rev)'}
                className="hero-pulse"
                style={{
                  animationDelay: `-${p.delay}s`,
                  ['--ht-dur' as string]: `${p.duration}s`,
                  ['--ht-len' as string]: `${p.pulseLen}`,
                }}
              />
            ))}
          </g>

          {/* ── Layer 3 — hub markers at intersections ── */}
          <g
            fill="url(#trace-fwd)"
            filter="url(#hub-glow)"
          >
            {HUBS.map((h, i) => (
              <circle
                key={`h-${i}`}
                cx={h.x}
                cy={h.y}
                r={2.4}
                className="hero-hub"
                style={{ animationDelay: `-${h.delay}s` }}
              />
            ))}
          </g>
        </g>
      </svg>
    </div>
  );
}

/* ───────────────────────── path data ───────────────────────── */

/** Layer 1 — long sweeping curves at very low opacity, never animated.
 *  Travel beyond the viewport so they appear to enter/exit naturally. */
const ATMOSPHERIC: string[] = [
  'M -60 220 C 200 100 600 320 860 180',
  'M -60 360 C 220 320 580 240 860 380',
  'M 200 -60 C 260 200 480 280 540 560',
  'M 860 100 C 600 220 200 280 -60 200',
  'M 620 -60 C 600 200 540 320 480 560',
];

/** Layer 2 — main network. Each carries a static guide + animated pulse.
 *  Mix of forward (copper→violet) and reverse gradient strokes,
 *  varied pulse length (`pulseLen` is % of pathLength=100) and timing.
 *  Path geometry deliberately routes around the central reading area
 *  but with peripheral intersections (paths cross at the perimeter). */
interface NetworkPath {
  d: string;
  dir: 'fwd' | 'rev';
  guideOpacity: number;
  pulseLen: number;
  delay: number;
  duration: number;
}

const NETWORK: NetworkPath[] = [
  // Top long sweep with subtle dip
  { d: 'M 20 80 C 160 30 280 130 380 100 C 500 70 620 130 760 80',
    dir: 'fwd', guideOpacity: 0.22, pulseLen: 8,  delay: 0,   duration: 11 },

  // Top-right diagonal flowing down-left, terminates near reading area edge
  { d: 'M 760 60 C 660 160 560 200 460 240 C 420 256 400 268 380 280',
    dir: 'rev', guideOpacity: 0.18, pulseLen: 6,  delay: 1.4, duration: 9.5 },

  // Bottom long sweep
  { d: 'M 20 460 C 220 400 380 460 480 440 C 600 416 700 460 780 420',
    dir: 'fwd', guideOpacity: 0.22, pulseLen: 10, delay: 2.4, duration: 12 },

  // Left ascender — left edge curve
  { d: 'M 80 60 C 30 180 30 340 60 380 C 80 420 120 460 160 460',
    dir: 'rev', guideOpacity: 0.20, pulseLen: 7,  delay: 3.2, duration: 9 },

  // Right ascender — right edge curve
  { d: 'M 740 80 C 770 200 770 340 740 380 C 720 420 690 460 660 460',
    dir: 'fwd', guideOpacity: 0.20, pulseLen: 7,  delay: 4.0, duration: 9.5 },

  // Cross-diagonal — lower-left to upper-right (crosses #2)
  { d: 'M 80 380 C 240 320 380 320 460 320 C 580 320 680 280 760 240',
    dir: 'rev', guideOpacity: 0.18, pulseLen: 12, delay: 4.8, duration: 11 },

  // Branch off the top sweep — short curl
  { d: 'M 380 100 C 410 140 460 140 480 130',
    dir: 'fwd', guideOpacity: 0.16, pulseLen: 5,  delay: 5.6, duration: 6 },

  // Branch off the bottom sweep — short curl
  { d: 'M 400 444 C 380 400 360 380 340 360',
    dir: 'rev', guideOpacity: 0.16, pulseLen: 5,  delay: 6.2, duration: 6.5 },

  // Top accent arc — connects to a top hub
  { d: 'M 240 60 C 320 30 380 30 440 70',
    dir: 'fwd', guideOpacity: 0.14, pulseLen: 14, delay: 7.0, duration: 7 },

  // Bottom accent arc
  { d: 'M 320 460 C 420 430 510 440 580 470',
    dir: 'rev', guideOpacity: 0.14, pulseLen: 14, delay: 7.6, duration: 7.5 },

  // Top-left corner detail — short flowing curl
  { d: 'M 60 80 C 100 100 140 90 200 110',
    dir: 'fwd', guideOpacity: 0.16, pulseLen: 7,  delay: 8.4, duration: 8 },

  // Top-right corner detail — mirrors top-left in feel
  { d: 'M 600 90 C 660 105 700 96 740 120',
    dir: 'rev', guideOpacity: 0.16, pulseLen: 7,  delay: 9.0, duration: 8.5 },

  // Bottom-right tail
  { d: 'M 540 460 C 580 440 620 444 680 462',
    dir: 'fwd', guideOpacity: 0.18, pulseLen: 9,  delay: 9.6, duration: 9 },

  // Left-side mid vertical accent — short, self-contained
  { d: 'M 200 60 C 230 90 250 130 240 180',
    dir: 'rev', guideOpacity: 0.14, pulseLen: 6,  delay: 10.2, duration: 7 },

  // Right-side mid descender — counterpart to the left accent
  { d: 'M 600 380 C 580 410 580 440 600 460',
    dir: 'fwd', guideOpacity: 0.16, pulseLen: 6,  delay: 10.8, duration: 7.5 },
];

/** Layer 3 — hub markers at deliberate peripheral intersection points. */
const HUBS: { x: number; y: number; delay: number }[] = [
  { x: 380, y: 100, delay: 0 },   // top-mid where #1 + #7 + #9 converge
  { x: 460, y: 240, delay: 1.2 }, // mid-right where #2 + #6 cross
  { x: 60,  y: 380, delay: 2.4 }, // left-mid where #4 + #6 meet
  { x: 740, y: 240, delay: 3.0 }, // right-mid where #5 + #6 meet
  { x: 380, y: 280, delay: 4.0 }, // mid-bottom-of-mid where #2 ends near #6
  { x: 200, y: 110, delay: 5.0 }, // top-left where new corner curl meets sweep
  { x: 600, y: 460, delay: 6.0 }, // bottom-right where new descender meets bottom
];
