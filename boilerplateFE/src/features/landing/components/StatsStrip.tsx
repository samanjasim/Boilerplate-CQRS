const STATS: { value: string; label: string }[] = [
  { value: '15', label: 'backend features' },
  { value: '22', label: 'frontend modules' },
  { value: '3', label: 'production clients' },
  { value: '0', label: 'hello-worlds' },
];

export function StatsStrip() {
  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-0 px-7 py-5 border-y border-border/30 surface-glass relative z-[2]">
      {STATS.map((s, i) => (
        <div
          key={s.label}
          className={`px-3 ${i < STATS.length - 1 ? 'md:border-r border-border/30' : ''}`}
        >
          <div className="text-[28px] font-light tracking-[-0.03em] leading-none font-display gradient-text">{s.value}</div>
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] mt-1.5 text-muted-foreground">{s.label}</div>
        </div>
      ))}
    </div>
  );
}
