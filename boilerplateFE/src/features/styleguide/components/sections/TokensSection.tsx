import { Section } from '../Section';

const SWATCH_GROUPS: { label: string; tokens: { name: string; cssVar: string }[] }[] = [
  {
    label: 'Primary scale (preset-driven)',
    tokens: ['50', '100', '200', '300', '400', '500', '600', '700', '800', '900', '950'].map((s) => ({
      name: `primary-${s}`,
      cssVar: `--color-primary-${s}`,
    })),
  },
  {
    label: 'Accent (emerald)',
    tokens: ['400', '500', '600', '700'].map((s) => ({
      name: `accent-${s}`,
      cssVar: `--color-accent-${s}`,
    })),
  },
  {
    label: 'Violet companion (info / spectrum cool axis)',
    tokens: ['300', '400', '500', '600', '700'].map((s) => ({
      name: `violet-${s}`,
      cssVar: `--color-violet-${s}`,
    })),
  },
  {
    label: 'Amber companion (warning / feature-icon rotation)',
    tokens: ['400', '500', '600', '700'].map((s) => ({
      name: `amber-${s}`,
      cssVar: `--color-amber-${s}`,
    })),
  },
];

const COMPOSITE_TOKENS: { name: string; cssVar: string; render: 'gradient' | 'shadow' | 'glass' }[] = [
  { name: 'aurora-1', cssVar: '--aurora-1', render: 'gradient' },
  { name: 'aurora-2', cssVar: '--aurora-2', render: 'gradient' },
  { name: 'aurora-3', cssVar: '--aurora-3', render: 'gradient' },
  { name: 'aurora-corner', cssVar: '--aurora-corner', render: 'gradient' },
  { name: 'spectrum-text', cssVar: '--spectrum-text', render: 'gradient' },
  { name: 'btn-primary-gradient', cssVar: '--btn-primary-gradient', render: 'gradient' },
  { name: 'glow-primary-sm', cssVar: '--glow-primary-sm', render: 'shadow' },
  { name: 'glow-primary-md', cssVar: '--glow-primary-md', render: 'shadow' },
  { name: 'glow-primary-lg', cssVar: '--glow-primary-lg', render: 'shadow' },
  { name: 'surface-glass', cssVar: '--surface-glass', render: 'glass' },
  { name: 'surface-glass-strong', cssVar: '--surface-glass-strong', render: 'glass' },
];

export function TokensSection() {
  return (
    <Section
      id="tokens"
      eyebrow="Tokens"
      title="Color, gradient, glow, glass"
      deck="Every token is defined in src/styles/index.css. Color scales are runtime-written by useThemePreset; composite tokens use color-mix() and var() to follow the active preset automatically."
    >
      {SWATCH_GROUPS.map((group) => (
        <div key={group.label}>
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-2">{group.label}</div>
          <div className="grid grid-cols-6 gap-2 lg:grid-cols-11">
            {group.tokens.map((t) => (
              <div key={t.name} className="rounded-md border border-border overflow-hidden">
                <div className="h-12" style={{ background: `var(${t.cssVar})` }} />
                <div className="p-2 text-[10px] font-mono text-muted-foreground">{t.name}</div>
              </div>
            ))}
          </div>
        </div>
      ))}

      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-2">
          Composite tokens
        </div>
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4">
          {COMPOSITE_TOKENS.map((t) => (
            <div key={t.name} className="rounded-md border border-border overflow-hidden bg-card">
              {t.render === 'gradient' && (
                <div className="h-20" style={{ background: `var(${t.cssVar})` }} />
              )}
              {t.render === 'shadow' && (
                <div className="h-20 flex items-center justify-center bg-card">
                  <div className="h-8 w-8 rounded-md bg-primary" style={{ boxShadow: `var(${t.cssVar})` }} />
                </div>
              )}
              {t.render === 'glass' && (
                <div className="aurora-canvas h-20 relative">
                  <div
                    className="absolute inset-3 rounded-md"
                    style={{ background: `var(${t.cssVar})`, border: '1px solid var(--border-strong)', backdropFilter: 'blur(20px)' }}
                  />
                </div>
              )}
              <div className="p-2 text-[10px] font-mono text-muted-foreground">{t.name}</div>
            </div>
          ))}
        </div>
      </div>
    </Section>
  );
}
