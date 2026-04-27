import { useMemo } from 'react';

interface PulseGridProps {
  /** Cell pitch in px. Default 32. */
  cell?: number;
  /** Fraction of cells that pulse (0..1). Default 0.18 (≈ 1 in 5.5). */
  density?: number;
  /** Pulse animation duration. Default 4200ms. */
  duration?: number;
}

/**
 * Visible "data network" — a grid of small squares where ~18% pulse on/off
 * at staggered times, plus a periodic scan wave that sweeps left-to-right.
 *
 * Place inside an aurora-canvas (or any positioned ancestor); absolutely
 * positioned and pointer-events:none. Mask: center-vignette so it stays
 * out of the way at the edges.
 */
export function PulseGrid({ cell = 32, density = 0.18, duration = 4200 }: PulseGridProps) {
  const COLS = 48;
  const ROWS = 28;

  // Deterministic pseudo-random pulse pattern (so re-renders don't reshuffle)
  const cells = useMemo(() => {
    const total = COLS * ROWS;
    const pulseCount = Math.floor(total * density);
    const seen = new Set<number>();
    const arr: { x: number; y: number; delay: number; size: number; filled: boolean }[] = [];
    let attempts = 0;
    while (arr.length < pulseCount && attempts < pulseCount * 4) {
      attempts++;
      const r1 = Math.abs(Math.sin(attempts * 12.9898) * 43758.5453) % 1;
      const idx = Math.floor(r1 * total);
      if (seen.has(idx)) continue;
      seen.add(idx);
      const x = idx % COLS;
      const y = Math.floor(idx / COLS);
      const r2 = Math.abs(Math.sin(attempts * 78.233) * 12345.6789) % 1;
      const r3 = Math.abs(Math.sin(attempts * 39.346) * 98765.4321) % 1;
      const delay = r2; // 0..1 fraction of duration
      const size = 3.2 + r3 * 1.6; // 3.2..4.8 px square
      const filled = r3 > 0.55;
      arr.push({ x, y, delay, size, filled });
    }
    return arr;
  }, [density]);

  return (
    <div
      aria-hidden
      className="pointer-events-none absolute inset-0 overflow-hidden"
      style={{ zIndex: 0 }}
    >
      <svg
        className="absolute inset-0 w-full h-full"
        style={{
          maskImage:
            'radial-gradient(ellipse 75% 65% at center, black 25%, transparent 85%)',
          WebkitMaskImage:
            'radial-gradient(ellipse 75% 65% at center, black 25%, transparent 85%)',
        }}
      >
        {/* Cells */}
        {cells.map((c, i) => {
          const px = c.x * cell + cell / 2 - c.size / 2;
          const py = c.y * cell + cell / 2 - c.size / 2;
          return (
            <rect
              key={i}
              x={px}
              y={py}
              width={c.size}
              height={c.size}
              rx="0.5"
              fill={c.filled ? 'currentColor' : 'none'}
              stroke={c.filled ? 'none' : 'currentColor'}
              strokeWidth={c.filled ? 0 : 0.6}
              className="pulse-grid-cell text-primary"
              style={{
                animationDelay: `-${c.delay * (duration / 1000)}s`,
                animationDuration: `${duration / 1000}s`,
              }}
            />
          );
        })}

        {/* Scan wave — a vertical band of slightly brighter squares sweeping across */}
        <g className="pulse-grid-scan">
          <rect
            x="-50"
            y="0"
            width="120"
            height={ROWS * cell}
            fill="url(#scan-grad)"
            opacity="0.9"
          />
          <defs>
            <linearGradient id="scan-grad" x1="0" x2="1" y1="0" y2="0">
              <stop
                offset="0%"
                stopColor="var(--color-primary)"
                stopOpacity="0"
              />
              <stop
                offset="50%"
                stopColor="var(--color-primary)"
                stopOpacity="0.18"
              />
              <stop
                offset="100%"
                stopColor="var(--color-primary)"
                stopOpacity="0"
              />
            </linearGradient>
          </defs>
        </g>
      </svg>
    </div>
  );
}
