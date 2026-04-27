const TAGS = [
  '.NET 10', 'React 19', 'Tailwind 4', 'Flutter 3', 'PostgreSQL',
  'Redis', 'RabbitMQ', 'MediatR', 'EF Core', 'MassTransit', 'OpenTelemetry',
];

export function TechStrip() {
  return (
    <div className="px-7 py-4 border-y border-border/30 flex flex-wrap items-center gap-3 text-[11px] surface-glass relative z-[2]">
      <span className="text-[9px] font-bold uppercase tracking-[0.2em] text-muted-foreground">Built on</span>
      {TAGS.map((tag) => (
        <span
          key={tag}
          className="px-2.5 py-1 rounded-md text-[11px] font-mono bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)] border border-[var(--border-strong)]"
        >
          {tag}
        </span>
      ))}
    </div>
  );
}
