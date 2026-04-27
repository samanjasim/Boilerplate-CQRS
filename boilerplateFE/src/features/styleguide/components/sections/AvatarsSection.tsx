import { toast } from 'sonner';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Section } from '../Section';

export function AvatarsSection() {
  return (
    <Section
      id="avatars"
      eyebrow="Avatars · Spinners · Toasts · Separator"
      title="Small primitives"
      deck="Avatar fallback uses the copper gradient and square-ish radius matching the brand mark. Toast surfaces are glass with a left accent strip per variant."
    >
      <div className="flex flex-wrap items-center gap-4">
        <Avatar><AvatarFallback>SJ</AvatarFallback></Avatar>
        <Avatar><AvatarFallback>AB</AvatarFallback></Avatar>
        <Avatar>
          <AvatarImage src="https://i.pravatar.cc/40" alt="" />
          <AvatarFallback>—</AvatarFallback>
        </Avatar>
        <Separator orientation="vertical" className="h-8" />
        <div className="flex items-center gap-2">
          <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-current border-r-transparent text-primary" />
          <span className="text-sm text-muted-foreground">spinning…</span>
        </div>
      </div>
      <Separator />
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" onClick={() => toast.success('Saved successfully')}>toast.success</Button>
        <Button variant="outline" onClick={() => toast.error('Something went wrong')}>toast.error</Button>
        <Button variant="outline" onClick={() => toast.info('FYI — long action queued')}>toast.info</Button>
        <Button variant="outline" onClick={() => toast.warning('Approaching quota')}>toast.warning</Button>
      </div>
    </Section>
  );
}
