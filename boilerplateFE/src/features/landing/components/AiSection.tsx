import { useEffect, useState } from 'react';
import { Bot, Globe, Sparkles, Wrench } from 'lucide-react';
import { useReveal } from './useReveal';

const CAPABILITIES: { label: string; icon: typeof Bot }[] = [
  { label: 'RAG · offline eval harness', icon: Sparkles },
  { label: 'Provider-agnostic runtime', icon: Bot },
  { label: 'Per-tenant agents + tools', icon: Wrench },
  { label: 'Public widgets · anonymous persona', icon: Globe },
];

export function AiSection() {
  const head = useReveal<HTMLDivElement>();
  const grid = useReveal<HTMLDivElement>();
  const pills = useReveal<HTMLDivElement>();
  return (
    <section id="ai" className="relative">
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-28">
        <div ref={head.ref} data-revealed={head.revealed} className="reveal-up">
          <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3 inline-flex items-center gap-2">
            <span className="pulse-dot" />
            AI · embedded · tenant-authored
          </div>
          <h2 className="text-[34px] sm:text-[40px] font-light tracking-[-0.025em] leading-[1.12] mb-4 font-display max-w-[720px]">
            Not bolted on.
            <br />
            <em className="not-italic font-medium gradient-text">Built in.</em>
          </h2>
          <p className="text-[15px] leading-[1.6] max-w-[640px] mb-12 text-muted-foreground">
            Multi-tenant agents, a production RAG pipeline with offline eval, automatic tool discovery
            via <code className="font-mono text-[13px] px-1 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)]">[AiTool]</code>,
            and persona-aware safety. Ship a Tutor agent, a Brand Content agent, or your own day one.
          </p>
        </div>

        <div ref={grid.ref} data-revealed={grid.revealed} className="reveal-stagger grid gap-5 lg:grid-cols-[1.15fr_1fr_1fr]">
          <ChatPreview />
          <ToolsPreview />
          <PersonasPreview />
        </div>

        <div ref={pills.ref} data-revealed={pills.revealed} className="reveal-stagger mt-10 flex flex-wrap gap-2.5">
          {CAPABILITIES.map((c) => {
            const Icon = c.icon;
            return (
              <span
                key={c.label}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[12px] font-medium surface-glass border border-[var(--border-strong)] text-foreground"
              >
                <Icon className="h-3.5 w-3.5 text-primary" />
                {c.label}
              </span>
            );
          })}
        </div>
      </div>
    </section>
  );
}

function ChatPreview() {
  const { ref, revealed } = useReveal<HTMLDivElement>(0.35);
  const [phase, setPhase] = useState<'typing' | 'response'>('typing');

  useEffect(() => {
    if (typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      setPhase('response');
      return;
    }
    if (!revealed) return;
    const t = setTimeout(() => setPhase('response'), 1400);
    return () => clearTimeout(t);
  }, [revealed]);

  return (
    <div ref={ref} className="surface-glass-strong rounded-2xl shadow-float overflow-hidden border border-border/50 hover-lift-card">
      <div className="flex items-center gap-2 px-4 py-2.5 border-b border-border/30 bg-[color-mix(in_srgb,var(--color-primary)_4%,transparent)]">
        <span className="flex gap-1.5">
          <span className="h-2.5 w-2.5 rounded-full bg-destructive/60" />
          <span className="h-2.5 w-2.5 rounded-full bg-[var(--color-amber-400)]" />
          <span className="h-2.5 w-2.5 rounded-full bg-[var(--color-accent-500)]" />
        </span>
        <span className="ml-2 inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-mono bg-[color-mix(in_srgb,var(--color-violet-500)_12%,transparent)] text-[var(--color-violet-700)] dark:text-[var(--color-violet-300)] border border-[color-mix(in_srgb,var(--color-violet-500)_22%,transparent)]">
          Student
        </span>
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-mono bg-[color-mix(in_srgb,var(--color-accent-500)_10%,transparent)] text-[var(--color-accent-700)] dark:text-[var(--color-accent-300)] border border-[color-mix(in_srgb,var(--color-accent-500)_22%,transparent)]">
          ChildSafe
        </span>
        <span className="ml-auto font-mono text-[10px] text-muted-foreground">tutor · gpt-4o</span>
      </div>

      <div className="p-5 space-y-3 text-[12.5px]">
        {/* User message */}
        <div className="flex justify-end">
          <div className="max-w-[80%] rounded-2xl rounded-br-sm px-3.5 py-2 bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-foreground">
            Explain photosynthesis like I'm 12.
          </div>
        </div>

        {/* Assistant message */}
        <div className="flex">
          <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg btn-primary-gradient glow-primary-sm mr-2.5">
            <Bot className="h-3.5 w-3.5 text-primary-foreground" />
          </div>
          <div className="flex-1 max-w-[88%] rounded-2xl rounded-bl-sm px-3.5 py-2.5 bg-card/60 border border-border/40 text-foreground leading-[1.55]">
            {phase === 'typing' ? (
              <div className="flex items-center gap-1.5 py-0.5" aria-label="Tutor is typing">
                <span className="text-[11px] text-muted-foreground mr-1">Tutor is thinking</span>
                <span className="typing-dot" style={{ animationDelay: '0ms' }} />
                <span className="typing-dot" style={{ animationDelay: '180ms' }} />
                <span className="typing-dot" style={{ animationDelay: '360ms' }} />
              </div>
            ) : (
              <>
                Plants use sunlight as fuel. Their leaves grab carbon dioxide from the air and water from
                the soil, then sunlight powers a tiny chemical kitchen inside the leaf that turns those
                ingredients into sugar (food) and oxygen<span className="caret-blink text-primary" />
                <div className="mt-3 flex flex-wrap gap-1.5">
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-mono bg-[color-mix(in_srgb,var(--color-violet-500)_8%,transparent)] text-[var(--color-violet-700)] dark:text-[var(--color-violet-300)] border border-[color-mix(in_srgb,var(--color-violet-500)_18%,transparent)]">
                    <span className="opacity-60">📖</span> Biology · Ch.4
                  </span>
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-mono bg-[color-mix(in_srgb,var(--color-violet-500)_8%,transparent)] text-[var(--color-violet-700)] dark:text-[var(--color-violet-300)] border border-[color-mix(in_srgb,var(--color-violet-500)_18%,transparent)]">
                    <span className="opacity-60">📖</span> Lab notes
                  </span>
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function ToolsPreview() {
  const tools = [
    { name: 'CreateTenantTool', module: 'Core' },
    { name: 'UpdateUserRoleTool', module: 'Core' },
    { name: 'ListInvoicesTool', module: 'Billing' },
    { name: 'ScheduleReportTool', module: 'Reports' },
    { name: 'PublishProductTool', module: 'Products' },
  ];
  return (
    <div className="surface-glass rounded-2xl p-5 hover-lift-card border border-border/40">
      <div className="text-[9px] font-bold uppercase tracking-[0.2em] text-primary mb-1">Auto-discovered</div>
      <h3 className="text-[15px] font-semibold text-foreground mb-1 font-display tracking-tight">Tools</h3>
      <p className="text-[11px] text-muted-foreground leading-[1.5] mb-4">
        Drop a class with <code className="font-mono text-[10px] px-1 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)]">[AiTool]</code> and it's available to every agent.
      </p>
      <div className="space-y-1.5">
        {tools.map((t) => (
          <div
            key={t.name}
            className="flex items-center justify-between text-[11.5px] px-2.5 py-1.5 rounded-md bg-card/50 border border-border/30"
          >
            <span className="font-mono text-foreground">{t.name}</span>
            <span className="font-mono text-[10px] text-muted-foreground">{t.module}</span>
          </div>
        ))}
        <div className="text-[10px] font-mono text-muted-foreground pl-2.5 pt-1">+ 8 more</div>
      </div>
    </div>
  );
}

function PersonasPreview() {
  const tenants = [
    {
      name: 'School',
      personas: [
        { name: 'Teacher', safety: 'Standard', tone: 'copper' as const },
        { name: 'Student', safety: 'ChildSafe', tone: 'emerald' as const },
        { name: 'Parent', safety: 'Standard', tone: 'copper' as const },
      ],
    },
    {
      name: 'Social',
      personas: [
        { name: 'Editor', safety: 'Standard', tone: 'copper' as const },
        { name: 'Approver', safety: 'Standard', tone: 'copper' as const },
        { name: 'Client', safety: 'Pro', tone: 'violet' as const },
      ],
    },
  ];

  const TONE: Record<'copper' | 'emerald' | 'violet', string> = {
    copper:
      'bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)] border-[color-mix(in_srgb,var(--color-primary)_22%,transparent)]',
    emerald:
      'bg-[color-mix(in_srgb,var(--color-accent-500)_8%,transparent)] text-[var(--color-accent-700)] dark:text-[var(--color-accent-300)] border-[color-mix(in_srgb,var(--color-accent-500)_22%,transparent)]',
    violet:
      'bg-[color-mix(in_srgb,var(--color-violet-500)_10%,transparent)] text-[var(--color-violet-700)] dark:text-[var(--color-violet-300)] border-[color-mix(in_srgb,var(--color-violet-500)_22%,transparent)]',
  };

  return (
    <div className="surface-glass rounded-2xl p-5 hover-lift-card border border-border/40">
      <div className="text-[9px] font-bold uppercase tracking-[0.2em] text-primary mb-1">Two-axis identity</div>
      <h3 className="text-[15px] font-semibold text-foreground mb-1 font-display tracking-tight">Persona × Role</h3>
      <p className="text-[11px] text-muted-foreground leading-[1.5] mb-4">
        Personas configure AI experience + safety. Roles configure CRUD. Orthogonal.
      </p>
      <div className="space-y-3">
        {tenants.map((tenant) => (
          <div key={tenant.name}>
            <div className="text-[9px] font-bold uppercase tracking-[0.18em] text-muted-foreground mb-1.5">
              {tenant.name}
            </div>
            <div className="flex flex-wrap gap-1">
              {tenant.personas.map((p) => (
                <span
                  key={p.name}
                  className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-mono border ${TONE[p.tone]}`}
                >
                  {p.name} <span className="opacity-60">·</span> {p.safety}
                </span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
