/**
 * Faint blueprint behind the architecture section: two abstract boxes joined by
 * connection lines that draw → hold → erase → redraw on a slow loop. Center-
 * vignetted, very low opacity so the actual architecture cards stay primary.
 *
 * Pure SVG + CSS via `.blueprint-line` and `.blueprint-node` (defined in index.css).
 * Each line carries `--bp-len` matching its drawn path length so stroke-dasharray
 * cycles cleanly.
 */
export function BlueprintBackground() {
  return (
    <div
      aria-hidden
      className="pointer-events-none absolute inset-0 overflow-hidden"
      style={{ zIndex: 0 }}
    >
      <svg
        className="absolute inset-0 w-full h-full"
        viewBox="0 0 800 400"
        preserveAspectRatio="xMidYMid slice"
        style={{
          maskImage:
            'radial-gradient(ellipse 80% 70% at center, black 20%, transparent 80%)',
          WebkitMaskImage:
            'radial-gradient(ellipse 80% 70% at center, black 20%, transparent 80%)',
          opacity: 0.55,
        }}
      >
        {/* Box 1 (top-left) — 4 sides drawn with staggered delays */}
        <g stroke="currentColor" strokeWidth="0.9" fill="none" className="text-primary">
          <line x1="80"  y1="80"  x2="280" y2="80"  className="blueprint-line" style={{ ['--bp-len' as string]: '200', animationDelay: '0s' }} />
          <line x1="280" y1="80"  x2="280" y2="220" className="blueprint-line" style={{ ['--bp-len' as string]: '140', animationDelay: '0.5s' }} />
          <line x1="280" y1="220" x2="80"  y2="220" className="blueprint-line" style={{ ['--bp-len' as string]: '200', animationDelay: '1.0s' }} />
          <line x1="80"  y1="220" x2="80"  y2="80"  className="blueprint-line" style={{ ['--bp-len' as string]: '140', animationDelay: '1.5s' }} />
        </g>

        {/* Box 2 (bottom-right) */}
        <g stroke="currentColor" strokeWidth="0.9" fill="none" className="text-primary">
          <line x1="520" y1="180" x2="720" y2="180" className="blueprint-line" style={{ ['--bp-len' as string]: '200', animationDelay: '2.5s' }} />
          <line x1="720" y1="180" x2="720" y2="320" className="blueprint-line" style={{ ['--bp-len' as string]: '140', animationDelay: '3.0s' }} />
          <line x1="720" y1="320" x2="520" y2="320" className="blueprint-line" style={{ ['--bp-len' as string]: '200', animationDelay: '3.5s' }} />
          <line x1="520" y1="320" x2="520" y2="180" className="blueprint-line" style={{ ['--bp-len' as string]: '140', animationDelay: '4.0s' }} />
        </g>

        {/* Connector diagonals */}
        <g stroke="currentColor" strokeWidth="0.9" fill="none" className="text-primary">
          <line x1="280" y1="120" x2="520" y2="200" className="blueprint-line" style={{ ['--bp-len' as string]: '260', animationDelay: '5.0s' }} />
          <line x1="280" y1="180" x2="520" y2="280" className="blueprint-line" style={{ ['--bp-len' as string]: '260', animationDelay: '6.0s' }} />
        </g>

        {/* Corner nodes (always visible, gentle pulse) */}
        <g fill="currentColor" className="text-primary">
          {[
            { x: 80,  y: 80,  d: 0 },
            { x: 280, y: 80,  d: 0.5 },
            { x: 280, y: 220, d: 1.0 },
            { x: 80,  y: 220, d: 1.5 },
            { x: 520, y: 180, d: 2.0 },
            { x: 720, y: 180, d: 2.5 },
            { x: 720, y: 320, d: 3.0 },
            { x: 520, y: 320, d: 3.5 },
          ].map((n, i) => (
            <circle
              key={i}
              cx={n.x}
              cy={n.y}
              r="2.4"
              className="blueprint-node"
              style={{ animationDelay: `-${n.d}s`, opacity: 0.4 }}
            />
          ))}
        </g>
      </svg>
    </div>
  );
}
