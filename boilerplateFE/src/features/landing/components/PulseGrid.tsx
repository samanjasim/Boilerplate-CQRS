import { useMemo } from 'react';

interface PulseGridProps {
  /** Cell size in px. Default 36. */
  cell?: number;
  /** Approximate fraction of cells that pulse. Default 0.10 (≈ 1 in 10). */
  density?: number;
  /** Total animation duration; staggered start times spread across this. Default 5500ms. */
  duration?: number;
}

/**
 * Ambient "data network breathing" — a faint dotted grid where ~10% of cells
 * have a slow opacity pulse. Lives behind hero/landing aurora as an extra layer
 * of liveness without grabbing attention. Pure CSS animation; no JS tick.
 *
 * Place inside an aurora-canvas (or any positioned ancestor); the component
 * is absolutely positioned and pointer-events:none.
 */
export function PulseGrid({ cell = 36, density = 0.1, duration = 5500 }: PulseGridProps) {
  // Generate a deterministic pseudo-random pulse pattern at module-evaluate time
  // (so re-renders don't reshuffle). Uses a simple hash-based picker.
  const cells = useMemo(() => {
    const COLS = 40;
    const ROWS = 30;
    const total = COLS * ROWS;
    const pulseCount = Math.floor(total * density);
    const arr: { x: number; y: number; delay: number }[] = [];
    for (let i = 0; i < pulseCount; i++) {
      // Deterministic pseudo-random: hash by index
      const r = Math.abs(Math.sin(i * 12.9898) * 43758.5453);
      const idx = Math.floor((r % 1) * total);
      const x = idx % COLS;
      const y = Math.floor(idx / COLS);
      const delay = ((i * 37) % 100) / 100; // 0..1 fraction of duration
      arr.push({ x, y, delay });
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
            'radial-gradient(ellipse 70% 60% at center, black 30%, transparent 80%)',
          WebkitMaskImage:
            'radial-gradient(ellipse 70% 60% at center, black 30%, transparent 80%)',
        }}
      >
        {cells.map((c, i) => (
          <circle
            key={i}
            cx={c.x * cell + cell / 2}
            cy={c.y * cell + cell / 2}
            r="1.4"
            fill="currentColor"
            className="pulse-grid-dot text-primary"
            style={{
              animationDelay: `-${c.delay * (duration / 1000)}s`,
              animationDuration: `${duration / 1000}s`,
            }}
          />
        ))}
      </svg>
    </div>
  );
}
