const CELLS: { label: string; name: string; meta: string }[] = [
  { label: 'Backend', name: '.NET 10', meta: 'CQRS · MediatR · EF Core' },
  { label: 'Frontend', name: 'React 19', meta: 'Tailwind 4 · TanStack · shadcn/ui' },
  { label: 'Mobile', name: 'Flutter 3', meta: 'flutter_bloc · Clean Arch · Hive' },
];

export function ArchitectureSection() {
  return (
    <section id="architecture" className="px-7 py-9 relative z-[2]">
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">Architecture at a glance</div>
      <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.15] mb-2 font-display">
        Three clients.<br />
        <em className="not-italic font-medium gradient-text">One source of truth.</em>
      </h2>
      <p className="text-[13px] leading-[1.55] max-w-[540px] mb-6 text-muted-foreground">
        Permission strings, theme tokens, and API response envelopes mirror across the .NET backend, React frontend, and Flutter mobile client. Define a permission once — it's enforced everywhere.
      </p>

      <div className="surface-glass rounded-xl p-5">
        <div className="grid gap-2 md:grid-cols-3 mb-4">
          {CELLS.map((c) => (
            <div key={c.label} className="bg-card/70 rounded-lg p-3 text-center border border-border/40">
              <div className="text-[9px] font-bold uppercase tracking-[0.18em] text-primary mb-1">{c.label}</div>
              <div className="text-[13px] font-semibold text-foreground mb-1">{c.name}</div>
              <div className="text-[10px] font-mono text-muted-foreground">{c.meta}</div>
            </div>
          ))}
        </div>
        <div className="text-center font-mono text-[10px] text-muted-foreground">
          <strong className="text-primary font-bold">API</strong> → <strong className="text-primary font-bold">Application</strong> (MediatR CQRS) → <strong className="text-primary font-bold">Domain</strong> ← <strong className="text-primary font-bold">Infrastructure</strong> (EF Core, Outbox, Services)
        </div>
      </div>
    </section>
  );
}
