import { Server, Smartphone, Monitor } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

const CELLS: { label: string; name: string; meta: string; icon: LucideIcon; accent: string }[] = [
  { label: 'Backend',  name: '.NET 10',   meta: 'CQRS · MediatR · EF Core',           icon: Server,     accent: 'btn-primary-gradient glow-primary-sm' },
  { label: 'Frontend', name: 'React 19',  meta: 'Tailwind 4 · TanStack · shadcn/ui',  icon: Monitor,    accent: 'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]' },
  { label: 'Mobile',   name: 'Flutter 3', meta: 'flutter_bloc · Clean Arch · Hive',   icon: Smartphone, accent: 'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]' },
];

export function ArchitectureSection() {
  return (
    <section id="architecture" className="relative">
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-24">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3">
          Architecture at a glance
        </div>
        <h2 className="text-[34px] sm:text-[40px] font-light tracking-[-0.025em] leading-[1.12] mb-4 font-display max-w-[720px]">
          Three clients.
          <br />
          <em className="not-italic font-medium gradient-text">One source of truth.</em>
        </h2>
        <p className="text-[15px] leading-[1.6] max-w-[600px] mb-12 text-muted-foreground">
          Permission strings, theme tokens, and API response envelopes mirror across the .NET backend,
          React frontend, and Flutter mobile client. Define a permission once — it's enforced everywhere.
        </p>

        <div className="surface-glass rounded-2xl p-6 lg:p-8 shadow-card">
          <div className="grid gap-4 md:grid-cols-3 mb-7">
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

          {/* Flow diagram */}
          <div className="border-t border-border/30 pt-5">
            <div className="text-center font-mono text-[11px] text-muted-foreground flex flex-wrap items-center justify-center gap-x-2 gap-y-1">
              <strong className="text-primary font-bold">API</strong>
              <span className="text-primary/60">→</span>
              <strong className="text-primary font-bold">Application</strong>
              <span className="text-muted-foreground/70">(MediatR CQRS)</span>
              <span className="text-primary/60">→</span>
              <strong className="text-primary font-bold">Domain</strong>
              <span className="text-primary/60">←</span>
              <strong className="text-primary font-bold">Infrastructure</strong>
              <span className="text-muted-foreground/70">(EF Core, Outbox)</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
