import type { ReactNode } from 'react';

interface SectionProps {
  id: string;
  eyebrow: string;
  title: string;
  deck?: string;
  children: ReactNode;
}

export function Section({ id, eyebrow, title, deck, children }: SectionProps) {
  return (
    <section id={id} className="scroll-mt-20 border-t border-border first:border-t-0 py-12">
      <div className="text-[11px] font-bold uppercase tracking-[0.18em] text-primary mb-2">{eyebrow}</div>
      <h2 className="text-2xl font-light tracking-tight text-foreground mb-2">{title}</h2>
      {deck ? <p className="text-sm text-muted-foreground max-w-prose mb-6">{deck}</p> : null}
      <div className="space-y-6">{children}</div>
    </section>
  );
}
