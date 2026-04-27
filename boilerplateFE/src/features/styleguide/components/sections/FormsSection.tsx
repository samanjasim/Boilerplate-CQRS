import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Section } from '../Section';

export function FormsSection() {
  return (
    <Section
      id="forms"
      eyebrow="Forms"
      title="Input · Textarea"
      deck="Both use --surface-glass background with a backdrop blur, so the aurora bleeds through subtly. Hairline border + soft focus ring."
    >
      <div className="grid gap-6 md:grid-cols-2">
        <div className="space-y-3">
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground">Input</div>
          <Input placeholder="Default" />
          <Input defaultValue="Filled value" />
          <Input placeholder="Disabled" disabled />
          <Input type="email" placeholder="email@example.com" />
        </div>
        <div className="space-y-3">
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground">Textarea</div>
          <Textarea placeholder="Default textarea — type something." />
          <Textarea defaultValue="Filled textarea." />
          <Textarea placeholder="Disabled" disabled />
        </div>
      </div>
    </Section>
  );
}
