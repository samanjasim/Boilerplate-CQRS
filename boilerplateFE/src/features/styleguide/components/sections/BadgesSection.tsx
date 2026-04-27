import { Badge } from '@/components/ui/badge';
import { Section } from '../Section';

export function BadgesSection() {
  return (
    <Section
      id="badges"
      eyebrow="Badges"
      title="Existing variants + J4 status pills"
      deck="default / secondary / destructive / outline are unchanged. J4 adds healthy (emerald), pending (amber), failed (destructive), and info (violet) for consistent status semantics across feature pages."
    >
      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">Existing</div>
        <div className="flex flex-wrap gap-2">
          <Badge>default</Badge>
          <Badge variant="secondary">secondary</Badge>
          <Badge variant="destructive">destructive</Badge>
          <Badge variant="outline">outline</Badge>
        </div>
      </div>
      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">J4 status pills</div>
        <div className="flex flex-wrap gap-2">
          <Badge variant="healthy">Healthy</Badge>
          <Badge variant="pending">Pending</Badge>
          <Badge variant="failed">Failed</Badge>
          <Badge variant="info">Info</Badge>
        </div>
      </div>
    </Section>
  );
}
