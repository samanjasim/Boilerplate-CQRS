import { Server, Monitor, Smartphone, Database, Cloud } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

type Tone = 'copper' | 'violet' | 'amber' | 'emerald' | 'primary-deep';

interface Pillar {
  label: string;
  title: string;
  meta: string;
  tags: string[];
  icon: LucideIcon;
  tone: Tone;
}

const PILLARS: Pillar[] = [
  {
    label: 'Backend',
    title: '.NET 10',
    meta: 'Clean Architecture · CQRS',
    tags: ['MediatR', 'EF Core', 'MassTransit', 'FluentValidation'],
    icon: Server,
    tone: 'copper',
  },
  {
    label: 'Frontend',
    title: 'React 19',
    meta: 'Tailwind 4 · TypeScript-strict',
    tags: ['TanStack', 'shadcn/ui', 'Zustand', 'i18n + RTL'],
    icon: Monitor,
    tone: 'violet',
  },
  {
    label: 'Mobile',
    title: 'Flutter 3',
    meta: 'Clean Architecture',
    tags: ['flutter_bloc', 'Hive', 'go_router', 'freezed'],
    icon: Smartphone,
    tone: 'emerald',
  },
];

const SUBSTRATE: { label: string; icon: LucideIcon; items: string[] }[] = [
  { label: 'Data plane', icon: Database, items: ['PostgreSQL', 'Redis', 'RabbitMQ'] },
  { label: 'Observability', icon: Cloud, items: ['OpenTelemetry → Jaeger', 'Prometheus', 'Serilog'] },
];

const TONE_BG: Record<Tone, string> = {
  copper: 'btn-primary-gradient glow-primary-sm',
  violet: 'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]',
  amber: 'bg-gradient-to-br from-[var(--color-amber-400)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_30%,transparent)]',
  emerald: 'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]',
  'primary-deep': 'bg-gradient-to-br from-[var(--color-primary-700)] to-[var(--color-primary-900)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-primary)_30%,transparent)]',
};

export function TechStrip() {
  return (
    <section className="relative">
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-24 border-y border-border/30">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3">
          Tech foundation
        </div>
        <h2 className="text-[28px] sm:text-[34px] font-light tracking-[-0.025em] leading-[1.15] mb-3 font-display max-w-[720px]">
          Latest of every stack.
          <br />
          <em className="not-italic font-medium gradient-text">Battle-tested patterns under it.</em>
        </h2>
        <p className="text-[14px] leading-[1.6] max-w-[560px] mb-10 text-muted-foreground">
          Three production clients, one source of truth. Each stack carries the patterns the other two
          can rely on without translation.
        </p>

        {/* 3 client pillars */}
        <div className="grid gap-4 md:grid-cols-3 mb-5">
          {PILLARS.map((p) => {
            const Icon = p.icon;
            return (
              <div key={p.label} className="surface-glass hover-lift-card rounded-2xl p-5 border border-border/40">
                <div className="flex items-center gap-3 mb-4">
                  <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-white ${TONE_BG[p.tone]}`}>
                    <Icon className="h-5 w-5" strokeWidth={2} />
                  </div>
                  <div>
                    <div className="text-[9px] font-bold uppercase tracking-[0.2em] text-primary">{p.label}</div>
                    <div className="text-[16px] font-semibold text-foreground font-display">{p.title}</div>
                  </div>
                </div>
                <div className="text-[11px] text-muted-foreground mb-3">{p.meta}</div>
                <div className="flex flex-wrap gap-1.5">
                  {p.tags.map((t) => (
                    <span
                      key={t}
                      className="font-mono text-[10px] px-2 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)] border border-[var(--border-strong)]"
                    >
                      {t}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>

        {/* Substrate row */}
        <div className="grid gap-4 md:grid-cols-2">
          {SUBSTRATE.map((s) => {
            const Icon = s.icon;
            return (
              <div key={s.label} className="surface-glass hover-lift-card rounded-2xl px-5 py-4 border border-border/40 flex items-center gap-4">
                <div className="w-9 h-9 shrink-0 rounded-xl flex items-center justify-center bg-card/60 border border-border/40 text-primary">
                  <Icon className="h-4 w-4" strokeWidth={2} />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="text-[9px] font-bold uppercase tracking-[0.2em] text-primary mb-0.5">{s.label}</div>
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    {s.items.map((it, i) => (
                      <span key={it} className="inline-flex items-center gap-2 font-mono text-[10px] text-foreground">
                        {it}
                        {i < s.items.length - 1 && (
                          <span className="text-muted-foreground/40">·</span>
                        )}
                      </span>
                    ))}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
