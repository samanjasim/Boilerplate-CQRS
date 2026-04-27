import { Section } from '../Section';

const SAMPLES: { label: string; className: string; sample: string; gradient?: boolean }[] = [
  { label: 'Hero metric (Inter 84px / 200)', className: 'text-[84px] font-extralight tracking-[-0.05em] leading-[0.95] font-display', sample: '142' },
  { label: 'Hero headline (Inter 44px / 200)', className: 'text-[44px] font-extralight tracking-[-0.04em] leading-[1.05] font-display', sample: 'Build what’s actually yours.' },
  { label: 'Hero gradient accent', className: 'text-[44px] font-medium tracking-[-0.04em] leading-[1.05] font-display gradient-text', sample: 'next-gen platform', gradient: true },
  { label: 'Section title (Inter 26px / 300)', className: 'text-[26px] font-light tracking-tight leading-[1.15] font-display', sample: 'Eight things every SaaS needs.' },
  { label: 'Page title (Inter 22px / 600)', className: 'text-[22px] font-semibold tracking-tight font-display', sample: 'Tenants' },
  { label: 'h1 (IBM Plex 1.75rem / 700)', className: 'text-[1.75rem] font-bold tracking-tight', sample: 'Heading 1' },
  { label: 'h2 (IBM Plex 1.25rem / 600)', className: 'text-[1.25rem] font-semibold', sample: 'Heading 2' },
  { label: 'h3 (1.0625rem / 600)', className: 'text-[1.0625rem] font-semibold', sample: 'Heading 3' },
  { label: 'h4 (0.9375rem / 600)', className: 'text-[0.9375rem] font-semibold', sample: 'Heading 4' },
  { label: 'Body (0.8125rem)', className: 'text-[0.8125rem] leading-[1.6]', sample: 'Body text. The quick brown fox jumps over the lazy dog.' },
  { label: 'Small (0.75rem)', className: 'text-[0.75rem]', sample: 'Small text — captions and metadata.' },
  { label: 'Eyebrow (10px uppercase 0.18em)', className: 'text-[10px] font-bold uppercase tracking-[0.18em] text-muted-foreground', sample: 'Section label' },
  { label: 'Mono numbers (IBM Plex Mono 12px)', className: 'text-[12px] font-mono text-foreground', sample: '142  3,847  $48.2K  +8.2%' },
  { label: 'Mono IDs (IBM Plex Mono 11px)', className: 'text-[11px] font-mono text-muted-foreground', sample: 'acme-corp  globex-ind  initech-sys' },
];

export function TypographySection() {
  return (
    <Section
      id="typography"
      eyebrow="Typography"
      title="Type ramp"
      deck="Inter for hero/section titles (font-display utility). IBM Plex Sans for body and UI. IBM Plex Sans Arabic auto-swaps in RTL. IBM Plex Mono for numbers and IDs."
    >
      <div className="space-y-5">
        {SAMPLES.map((s) => (
          <div key={s.label} className="grid grid-cols-1 gap-2 md:grid-cols-[180px_1fr] md:gap-6 md:items-baseline">
            <div className="text-[10px] font-mono text-muted-foreground">{s.label}</div>
            <div>
              <span className={s.className}>{s.sample}</span>
            </div>
          </div>
        ))}
      </div>
    </Section>
  );
}
