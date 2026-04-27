const TAGS: { label: string; group: 'be' | 'fe' | 'mobile' | 'data' | 'ops' }[] = [
  { label: '.NET 10', group: 'be' },
  { label: 'React 19', group: 'fe' },
  { label: 'Tailwind 4', group: 'fe' },
  { label: 'Flutter 3', group: 'mobile' },
  { label: 'PostgreSQL', group: 'data' },
  { label: 'Redis', group: 'data' },
  { label: 'RabbitMQ', group: 'data' },
  { label: 'MediatR', group: 'be' },
  { label: 'EF Core', group: 'be' },
  { label: 'MassTransit', group: 'be' },
  { label: 'OpenTelemetry', group: 'ops' },
];

const GROUP_DOT: Record<string, string> = {
  be: 'bg-[var(--color-primary)]',
  fe: 'bg-[var(--color-violet-500)]',
  mobile: 'bg-[var(--color-amber-500)]',
  data: 'bg-[var(--color-accent-500)]',
  ops: 'bg-[var(--color-primary-700)]',
};

export function TechStrip() {
  return (
    <div className="border-y border-border/30 surface-glass relative">
      <div className="mx-auto max-w-6xl px-7 py-5 flex flex-wrap items-center gap-x-3 gap-y-2 text-[11px]">
        <span className="text-[9px] font-bold uppercase tracking-[0.2em] text-muted-foreground mr-2">
          Built on
        </span>
        {TAGS.map((tag) => (
          <span
            key={tag.label}
            className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-[11px] font-mono bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)] border border-[var(--border-strong)] transition-all hover:border-[color-mix(in_srgb,var(--color-primary)_30%,transparent)] hover:bg-[color-mix(in_srgb,var(--color-primary)_12%,transparent)]"
          >
            <span className={`h-1.5 w-1.5 rounded-full ${GROUP_DOT[tag.group]}`} />
            {tag.label}
          </span>
        ))}
      </div>
    </div>
  );
}
