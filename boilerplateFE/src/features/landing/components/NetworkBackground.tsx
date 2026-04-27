import { useMemo } from 'react';

interface NetworkBackgroundProps {
  /** Number of nodes. Default 9. */
  nodes?: number;
  /** Approx fraction of node-pairs to connect (0..1). Default 0.35. */
  connectivity?: number;
}

/**
 * Constellation network behind the hero. Nodes pulse, links fade in/out.
 * No scan wave, no grid — just a faint interconnected graph. Center-vignetted
 * so edges fade. Pure SVG + CSS animations; respects prefers-reduced-motion
 * via the .net-node / .net-link rules in index.css.
 */
export function NetworkBackground({ nodes: nodeCount = 9, connectivity = 0.35 }: NetworkBackgroundProps) {
  const { nodes, links } = useMemo(() => {
    // Deterministic node placement (so re-renders don't reshuffle).
    // Spread nodes across an 800x500 virtual viewport with bias toward center.
    const W = 800;
    const H = 500;
    const arr: { x: number; y: number; delay: number }[] = [];
    for (let i = 0; i < nodeCount; i++) {
      const r1 = Math.abs(Math.sin(i * 12.9898 + 7) * 43758.5453) % 1;
      const r2 = Math.abs(Math.sin(i * 78.233 + 13) * 12345.6789) % 1;
      const r3 = Math.abs(Math.sin(i * 39.346 + 21) * 98765.4321) % 1;
      // Weight toward edges-but-not-corners
      const x = 60 + r1 * (W - 120);
      const y = 50 + r2 * (H - 100);
      arr.push({ x, y, delay: r3 * 3.6 });
    }

    // Build links: connect each node to nearest 2-3 neighbors, then drop some
    // by connectivity factor.
    const linkArr: { a: number; b: number; delay: number }[] = [];
    for (let i = 0; i < nodeCount; i++) {
      const distances = arr
        .map((n, j) => ({ j, d: Math.hypot(n.x - arr[i]!.x, n.y - arr[i]!.y) }))
        .filter((d) => d.j !== i)
        .sort((a, b) => a.d - b.d)
        .slice(0, 3);
      for (const { j } of distances) {
        if (i < j) {
          const r = Math.abs(Math.sin(i * 11 + j * 17) * 1000) % 1;
          if (r < connectivity) {
            const delay = (Math.abs(Math.sin(i * 31 + j * 7) * 100) % 1) * 5.4;
            linkArr.push({ a: i, b: j, delay });
          }
        }
      }
    }
    return { nodes: arr, links: linkArr };
  }, [nodeCount, connectivity]);

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
          maskImage:
            'radial-gradient(ellipse 70% 70% at center, black 30%, transparent 88%)',
          WebkitMaskImage:
            'radial-gradient(ellipse 70% 70% at center, black 30%, transparent 88%)',
        }}
      >
        {/* Links rendered first so nodes paint on top */}
        {links.map((l, i) => {
          const a = nodes[l.a]!;
          const b = nodes[l.b]!;
          return (
            <line
              key={`l-${i}`}
              x1={a.x}
              y1={a.y}
              x2={b.x}
              y2={b.y}
              stroke="currentColor"
              strokeWidth="0.8"
              className="net-link text-primary"
              style={{ animationDelay: `-${l.delay}s` }}
            />
          );
        })}
        {nodes.map((n, i) => (
          <circle
            key={`n-${i}`}
            cx={n.x}
            cy={n.y}
            r="2"
            fill="currentColor"
            className="net-node text-primary"
            style={{
              animationDelay: `-${n.delay}s`,
              filter: 'drop-shadow(0 0 4px color-mix(in srgb, var(--color-primary) 40%, transparent))',
            }}
          />
        ))}
      </svg>
    </div>
  );
}
