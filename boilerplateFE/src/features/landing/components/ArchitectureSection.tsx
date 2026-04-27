import { Server, Smartphone, Monitor, Layers } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useReveal } from './useReveal';

const CELLS: { label: string; name: string; meta: string; icon: LucideIcon; accent: string }[] = [
  { label: 'Backend',  name: '.NET 10',   meta: 'CQRS · MediatR · EF Core',           icon: Server,     accent: 'btn-primary-gradient glow-primary-sm' },
  { label: 'Frontend', name: 'React 19',  meta: 'Tailwind 4 · TanStack',              icon: Monitor,    accent: 'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]' },
  { label: 'Mobile',   name: 'Flutter 3', meta: 'flutter_bloc · Hive',                icon: Smartphone, accent: 'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]' },
  { label: 'Cross-cutting', name: 'Shared', meta: 'Auth · Audit · Webhooks · OTel',   icon: Layers,     accent: 'bg-gradient-to-br from-[var(--color-amber-400)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_30%,transparent)]' },
];

export function ArchitectureSection() {
  const head = useReveal<HTMLDivElement>();
  const cards = useReveal<HTMLDivElement>();
  return (
    <section id="architecture" className="relative">
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-24">
        <div ref={head.ref} data-revealed={head.revealed} className="reveal-up">
          <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3">
            Architecture at a glance
          </div>
          <h2 className="text-[34px] sm:text-[40px] font-light tracking-[-0.025em] leading-[1.12] mb-4 font-display max-w-[720px]">
            Three clients. One source of truth.
            <br />
            <em className="not-italic font-medium gradient-text">Events flow, not coupling.</em>
          </h2>
          <p className="text-[15px] leading-[1.6] max-w-[600px] mb-12 text-muted-foreground">
            Permission strings, theme tokens, and API response envelopes mirror across all three clients.
            Cross-cutting concerns ride a transactional outbox so business writes and integration events
            commit together — never one without the other.
          </p>
        </div>

        <div className="surface-glass rounded-2xl p-6 lg:p-8 shadow-card">
          <div ref={cards.ref} data-revealed={cards.revealed} className="reveal-stagger grid gap-4 md:grid-cols-2 lg:grid-cols-4 mb-8">
            {CELLS.map((c) => {
              const Icon = c.icon;
              return (
                <div
                  key={c.label}
                  className="hover-lift-card relative bg-card/60 rounded-xl p-5 text-center border border-border/40"
                >
                  <div className={`mx-auto mb-3 w-11 h-11 rounded-xl flex items-center justify-center text-white ${c.accent}`}>
                    <Icon className="h-5 w-5" strokeWidth={2} />
                  </div>
                  <div className="text-[9px] font-bold uppercase tracking-[0.2em] text-primary mb-1.5">
                    {c.label}
                  </div>
                  <div className="text-[15px] font-semibold text-foreground mb-1.5 font-display">
                    {c.name}
                  </div>
                  <div className="text-[11px] font-mono text-muted-foreground">{c.meta}</div>
                </div>
              );
            })}
          </div>

          {/* Animated event flow */}
          <EventFlowDiagram />
        </div>
      </div>
    </section>
  );
}

/** Simple horizontal flow with a copper→violet gradient pulse traveling left-to-right.
 *  Pure SVG + CSS animation — no JS, no external deps. */
function EventFlowDiagram() {
  return (
    <div className="border-t border-border/30 pt-6">
      <div className="text-[9px] font-bold uppercase tracking-[0.2em] text-muted-foreground mb-3">
        Event flow · API → Application → Domain ← Infrastructure
      </div>

      <div className="relative">
        <svg viewBox="0 0 800 60" className="w-full h-[60px]" preserveAspectRatio="none">
          <defs>
            <linearGradient id="flow-track" x1="0" x2="1" y1="0" y2="0">
              <stop offset="0%" stopColor="var(--color-primary-700)" stopOpacity="0.25" />
              <stop offset="50%" stopColor="var(--color-primary)" stopOpacity="0.4" />
              <stop offset="100%" stopColor="var(--color-violet-500)" stopOpacity="0.25" />
            </linearGradient>
            <linearGradient id="flow-pulse" x1="0" x2="1" y1="0" y2="0">
              <stop offset="0%" stopColor="var(--color-primary)" stopOpacity="0" />
              <stop offset="50%" stopColor="var(--color-primary)" stopOpacity="1" />
              <stop offset="100%" stopColor="var(--color-violet-500)" stopOpacity="0" />
            </linearGradient>
          </defs>

          {/* Static track */}
          <line x1="40" x2="760" y1="30" y2="30" stroke="url(#flow-track)" strokeWidth="2" strokeLinecap="round" />

          {/* Stations */}
          {[
            { x: 40, label: 'Controller' },
            { x: 220, label: 'MediatR' },
            { x: 400, label: 'Domain' },
            { x: 580, label: 'EF Core' },
            { x: 760, label: 'Outbox' },
          ].map((s) => (
            <g key={s.label}>
              <circle cx={s.x} cy="30" r="4" fill="var(--color-primary)" />
              <circle cx={s.x} cy="30" r="8" fill="var(--color-primary)" opacity="0.18" />
            </g>
          ))}

          {/* Animated pulse (traveling dot) */}
          <circle r="5" fill="var(--color-primary)" filter="drop-shadow(0 0 6px color-mix(in srgb, var(--color-primary) 60%, transparent))">
            <animate
              attributeName="cx"
              from="40"
              to="760"
              dur="3.6s"
              repeatCount="indefinite"
              calcMode="spline"
              keySplines="0.4 0 0.2 1"
            />
            <animate attributeName="cy" values="30;30" dur="3.6s" repeatCount="indefinite" />
          </circle>
        </svg>

        {/* Station labels under the SVG, aligned approximately */}
        <div className="grid grid-cols-5 gap-1 mt-1 px-1 text-[9px] font-mono text-muted-foreground">
          <span className="text-center">Controller</span>
          <span className="text-center">MediatR</span>
          <span className="text-center">Domain</span>
          <span className="text-center">EF Core</span>
          <span className="text-center">Outbox</span>
        </div>
      </div>
    </div>
  );
}
