import { Button } from '@/components/ui/button';
import { Section } from '../Section';

export function ButtonsSection() {
  return (
    <Section
      id="buttons"
      eyebrow="Buttons"
      title="Variants × sizes"
      deck="default uses .btn-primary-gradient + .glow-primary-md. secondary uses .surface-glass. Other variants unchanged from baseline."
    >
      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">Variants (default size)</div>
        <div className="flex flex-wrap items-center gap-3">
          <Button>default</Button>
          <Button variant="destructive">destructive</Button>
          <Button variant="outline">outline</Button>
          <Button variant="secondary">secondary</Button>
          <Button variant="ghost">ghost</Button>
          <Button variant="link">link</Button>
        </div>
      </div>

      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">Sizes (default variant)</div>
        <div className="flex flex-wrap items-center gap-3">
          <Button size="sm">sm</Button>
          <Button size="default">default</Button>
          <Button size="lg">lg</Button>
        </div>
      </div>

      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">States</div>
        <div className="flex flex-wrap items-center gap-3">
          <Button disabled>disabled</Button>
          <Button variant="outline" disabled>outline disabled</Button>
        </div>
      </div>
    </Section>
  );
}
